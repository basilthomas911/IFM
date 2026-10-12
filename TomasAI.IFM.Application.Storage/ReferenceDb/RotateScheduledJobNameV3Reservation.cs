using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RotateScheduledJobNameV3Reservation(
    Guid reservationToken,
    string jobName,
    int jobId,
    Guid expectedReservationToken) : IBindValue
{
    public object Bind() => new object?[]
    {
        reservationToken,
        jobName,
        jobId,
        expectedReservationToken
    };
}
