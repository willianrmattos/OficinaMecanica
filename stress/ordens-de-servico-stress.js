import http from 'k6/http';
import { sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  vus: Number(__ENV.VUS || 20),
  duration: __ENV.DURATION || '2m',
};

export default function () {
  // Endpoint publico (AllowAnonymous) - nao precisa de login. O numero nem
  // precisa existir: mesmo dando 404, o handler ja bate no banco pra saber
  // disso, o suficiente pra gerar carga real de CPU/EF Core.
  http.get(`${BASE_URL}/api/ordens-de-servico/numero/OS-TESTE`);
  sleep(0.1);
}
