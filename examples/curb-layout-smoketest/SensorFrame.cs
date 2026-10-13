namespace LayoutSmoke;

internal sealed record SensorFrame(
    int Temperature,
    int Humidity,
    CalibrationKind Kind
)
{
    internal CalibrationPayload Payload => new(Temperature, Humidity);
}
