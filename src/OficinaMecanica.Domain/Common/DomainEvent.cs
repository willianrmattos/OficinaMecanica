using MediatR;

namespace OficinaMecanica.Domain.Common;

public abstract class DomainEvent : INotification
{
    public DateTime OcorridoEm { get; }

    protected DomainEvent()
    {
        OcorridoEm = DateTime.UtcNow;
    }
}
