#!/usr/bin/env python3
"""Ferramentas do e2e. So biblioteca padrao do Python mais o openssl que ja vem com o Git for Windows.

Subcomandos:
  gerar-chaves <dir>                      par RSA de DEV + JWKS publico (idempotente)
  emitir-token <dir> <perfil> [--sub X] [--minutos N]
  assinar-webhook <segredo> <data.id> <x-request-id> [<ts_ms>]
  rabbit-duplicar <url> <user> <senha> <fila>   copia identica (mesmo messageId) da 1a mensagem da fila, via management
  cpf | placa                             dados validos e aleatorios para os cadastros
  jget <caminho>                          le JSON do stdin e imprime o valor no caminho (a.b[0].c)
  jlen <caminho>                          tamanho de uma lista (ou de um objeto) no caminho
  jfind <caminho-da-lista> <chave> <valor> [<saida>]   primeiro item da lista com chave == valor
  jhistorico                              status do historicoStatus da OS em ordem cronologica, separados por ","
"""
import base64
import hashlib
import hmac
import json
import os
import random
import re
import subprocess
import sys
import time
import uuid

# Optei por reconfigurar a saida: no Windows o Python traduz "\n" em "\r\n" mesmo com pipe, e um "\r" perdido no
# valor capturado por $(...) no bash quebra qualquer comparacao de string.
sys.stdout.reconfigure(encoding="utf-8", newline="\n")
sys.stderr.reconfigure(encoding="utf-8", newline="\n")

ISSUER = os.environ.get("E2E_JWT_ISSUER", "OficinaMecanica.Seguranca")
AUDIENCE = os.environ.get("E2E_JWT_AUDIENCE", "OficinaMecanica.Client")
KID = "e2e-dev-1"

# O Seguranca real so emite o perfil Admin e nenhum controller le papel (todos sao [Authorize] simples; o mecanico da
# Execucao e o claim "sub"). Os demais perfis existem so para identificar quem fez cada chamada nos logs.
PERFIS = {
    "admin": ("00000000-0000-4000-8000-0000000000a1", "Admin"),
    "atendente": ("00000000-0000-4000-8000-0000000000a2", "Atendente"),
    "mecanico": ("00000000-0000-4000-8000-0000000000b1", "Mecanico"),
}


def b64url(dados: bytes) -> str:
    return base64.urlsafe_b64encode(dados).rstrip(b"=").decode("ascii")


def openssl(*args: str, entrada: bytes | None = None) -> bytes:
    resultado = subprocess.run(["openssl", *args], input=entrada, capture_output=True, check=False)
    if resultado.returncode != 0:
        sys.exit(f"openssl {' '.join(args)} falhou: {resultado.stderr.decode(errors='replace')}")
    return resultado.stdout


def gerar_chaves(diretorio: str) -> None:
    privada = os.path.join(diretorio, "jwt-privada.pem")
    jwks_dir = os.path.join(diretorio, "jwks")
    os.makedirs(jwks_dir, exist_ok=True)

    if not os.path.exists(privada):
        openssl("genrsa", "-out", privada, "2048")

    saida = openssl("rsa", "-in", privada, "-noout", "-modulus").decode()
    modulo_hex = re.search(r"Modulus=([0-9A-Fa-f]+)", saida).group(1)
    modulo = bytes.fromhex(modulo_hex)
    # genrsa gera sempre o expoente publico 65537 (AQAB em base64url).
    jwks = {"keys": [{"kty": "RSA", "use": "sig", "alg": "RS256", "kid": KID, "n": b64url(modulo), "e": "AQAB"}]}
    with open(os.path.join(jwks_dir, "jwks.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(jwks, f)
    print(f"chaves: {privada} (so local) | jwks: {os.path.join(jwks_dir, 'jwks.json')}")


def emitir_token(diretorio: str, perfil: str, sub: str | None, minutos: int) -> str:
    if perfil not in PERFIS:
        sys.exit(f"perfil desconhecido: {perfil} (use {', '.join(PERFIS)})")
    sub_padrao, papel = PERFIS[perfil]
    agora = int(time.time())
    cabecalho = {"alg": "RS256", "typ": "JWT", "kid": KID}
    corpo = {
        "sub": sub or sub_padrao,
        "name": f"e2e-{perfil}",
        "role": papel,
        "jti": str(uuid.uuid4()),
        "iat": agora,
        "exp": agora + minutos * 60,
        "iss": ISSUER,
        "aud": AUDIENCE,
    }
    a_assinar = f"{b64url(json.dumps(cabecalho, separators=(',', ':')).encode())}.{b64url(json.dumps(corpo, separators=(',', ':')).encode())}"
    assinatura = openssl("dgst", "-sha256", "-sign", os.path.join(diretorio, "jwt-privada.pem"), entrada=a_assinar.encode())
    return f"{a_assinar}.{b64url(assinatura)}"


def assinar_webhook(segredo: str, data_id: str, request_id: str, ts: str | None = None) -> str:
    # Mesmo manifesto do AssinaturaWebhookMercadoPago do Billing: id em minusculas, request-id e ts, cada um com ";".
    ts = ts or str(int(time.time() * 1000))
    manifesto = f"id:{data_id.lower()};request-id:{request_id};ts:{ts};"
    v1 = hmac.new(segredo.encode(), manifesto.encode(), hashlib.sha256).hexdigest()
    return f"ts={ts},v1={v1}"


def cpf_valido() -> str:
    while True:
        base = [random.randint(0, 9) for _ in range(9)]
        if len(set(base)) == 1:
            continue
        for tamanho in (9, 10):
            soma = sum(d * p for d, p in zip(base, range(tamanho + 1, 1, -1)))
            resto = (soma * 10) % 11
            base.append(0 if resto == 10 else resto)
        return "".join(map(str, base))


def placa_mercosul() -> str:
    letras = "ABCDEFGHJKLMNPRSTUVWXYZ"
    return f"{''.join(random.choice(letras) for _ in range(3))}{random.randint(0, 9)}{random.choice(letras)}{random.randint(0, 99):02d}"


def rabbit_duplicar(base: str, usuario: str, senha: str, fila: str) -> str:
    """Le (sem consumir) a primeira mensagem da fila pela API de management e republica uma copia identica, com o mesmo
    messageId do envelope: e o que um broker em at-least-once pode entregar de novo. Imprime o messageId copiado."""
    import urllib.request

    cabecalho = {
        "Authorization": "Basic " + base64.b64encode(f"{usuario}:{senha}".encode()).decode(),
        "Content-Type": "application/json",
    }

    def chamar(caminho: str, corpo: dict) -> object:
        pedido = urllib.request.Request(f"{base}/api/{caminho}", data=json.dumps(corpo).encode(), headers=cabecalho, method="POST")
        with urllib.request.urlopen(pedido, timeout=15) as resposta:
            return json.loads(resposta.read())

    mensagens = chamar(f"queues/%2F/{fila}/get", {"count": 1, "ackmode": "ack_requeue_true", "encoding": "auto", "truncate": 100000})
    if not mensagens:
        sys.exit(f"a fila {fila} esta vazia: nada para duplicar")
    original = mensagens[0]
    message_id = json.loads(original["payload"]).get("messageId", "")
    resultado = chamar("exchanges/%2F/amq.default/publish", {
        "properties": original["properties"],
        "routing_key": fila,
        "payload": original["payload"],
        "payload_encoding": original["payload_encoding"],
    })
    if not resultado.get("routed"):
        sys.exit("a copia nao foi roteada para a fila")
    return message_id


def _ler_json() -> object:
    bruto = sys.stdin.buffer.read().decode("utf-8-sig").strip()
    return json.loads(bruto) if bruto else None


def _caminho(dado: object, caminho: str) -> object:
    atual = dado
    for parte in [p for p in re.split(r"\.|(?=\[)", caminho) if p]:
        if atual is None:
            return None
        indice = re.fullmatch(r"\[(-?\d+)\]", parte)
        if indice:
            posicao = int(indice.group(1))
            atual = atual[posicao] if isinstance(atual, list) and -len(atual) <= posicao < len(atual) else None
        else:
            atual = atual.get(parte) if isinstance(atual, dict) else None
    return atual


def _formatar(valor: object) -> str:
    if valor is None:
        return ""
    if isinstance(valor, bool):
        return "true" if valor else "false"
    if isinstance(valor, (dict, list)):
        return json.dumps(valor, ensure_ascii=False, separators=(",", ":"))
    return str(valor)


def main(argv: list[str]) -> None:
    if len(argv) < 2:
        sys.exit(__doc__)
    comando, args = argv[1], argv[2:]

    if comando == "gerar-chaves":
        gerar_chaves(args[0])
    elif comando == "emitir-token":
        sub = args[args.index("--sub") + 1] if "--sub" in args else None
        minutos = int(args[args.index("--minutos") + 1]) if "--minutos" in args else 120
        print(emitir_token(args[0], args[1], sub, minutos))
    elif comando == "assinar-webhook":
        print(assinar_webhook(args[0], args[1], args[2], args[3] if len(args) > 3 else None))
    elif comando == "rabbit-duplicar":
        print(rabbit_duplicar(args[0], args[1], args[2], args[3]))
    elif comando == "cpf":
        print(cpf_valido())
    elif comando == "placa":
        print(placa_mercosul())
    elif comando == "jget":
        print(_formatar(_caminho(_ler_json(), args[0])))
    elif comando == "jlen":
        valor = _caminho(_ler_json(), args[0]) if args[0] else _ler_json()
        print(len(valor) if isinstance(valor, (list, dict)) else 0)
    elif comando == "jfind":
        lista = _caminho(_ler_json(), args[0]) or []
        achado = next((i for i in lista if isinstance(i, dict) and _formatar(i.get(args[1])) == args[2]), None)
        print(_formatar(_caminho(achado, args[3]) if len(args) > 3 else achado))
    elif comando == "jhistorico":
        itens = _caminho(_ler_json(), "historicoStatus") or []
        # A API devolve o historico sem ordenar por data; ordeno aqui para comparar a sequencia real das transicoes.
        print(",".join(i["status"] for i in sorted(itens, key=lambda i: i["dataAlteracao"])))
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)
