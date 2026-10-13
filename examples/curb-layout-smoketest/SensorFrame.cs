namespace LayoutSmoke;

internal sealed record SensorFrame(
    int Temperature,
    int Humidity,
    CalibrationKind Kind
);
