using System.Globalization;

namespace LayoutSmoke;

internal static class ConditionSample
{
    private const int MinimumCalibrationTemperature = 20;
    private const int MaximumCalibrationHumidity = 80;
    private const int RequiredPrimaryReferenceTemperature = 25;
    private const int RequiredSecondaryReferenceHumidity = 70;

    internal static bool IsWithinCalibrationBand(SensorFrame reading)
    {
        if (reading is { Temperature: > MinimumCalibrationTemperature, Humidity: < MaximumCalibrationHumidity } accepted) { return accepted.Temperature > accepted.Humidity; }
        return false;
    }

    internal static bool HasSameLabel(string primaryCalibrationLabel, string secondaryCalibrationLabel)
    {
        if (string.Equals(primaryCalibrationLabel.Trim(), secondaryCalibrationLabel.Trim(), StringComparison.OrdinalIgnoreCase)) { return true; }
        return false;
    }

    internal static bool IsTypedCalibrationBand(object reading)
    {
        if (reading is SensorFrame { Temperature: > MinimumCalibrationTemperature, Humidity: < MaximumCalibrationHumidity } accepted) { return accepted.Temperature > accepted.Humidity; }
        return false;
    }

    internal static bool IsNotCalibrationBand(object reading)
    {
        if (reading is not SensorFrame { Temperature: > MinimumCalibrationTemperature, Humidity: < MaximumCalibrationHumidity }) { return true; }
        return false;
    }

    internal static bool HasNestedCalibrationBand(SensorFrame reading)
    {
        if (reading is { Payload: { Temperature: > RequiredPrimaryReferenceTemperature, Humidity: < RequiredSecondaryReferenceHumidity } } accepted) { return accepted.Payload.Temperature > accepted.Payload.Humidity; }
        return false;
    }

    internal static bool MatchesReferenceShape(object reading)
    {
        if (reading is SensorFrame(RequiredPrimaryReferenceTemperature, RequiredSecondaryReferenceHumidity, CalibrationKind.BackupReference) { Temperature: > MinimumCalibrationTemperature }) { return true; }
        return false;
    }

    internal static bool IsReferenceKind(CalibrationKind kind)
    {
        if (kind is CalibrationKind.PrimaryReference or CalibrationKind.SecondaryReference or CalibrationKind.BackupReference) { return true; }
        return false;
    }

    internal static bool MatchesReferenceFrame(object reading)
    {
        if (reading is SensorFrame(RequiredPrimaryReferenceTemperature, RequiredSecondaryReferenceHumidity, CalibrationKind.BackupReference)) { return true; }
        return false;
    }

    internal static bool ParsesReading(bool usePrimary, string rawPrimaryReading, string rawSecondaryReading)
    {
        if (usePrimary ? int.TryParse(rawPrimaryReading, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) : int.TryParse(rawSecondaryReading, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) { return true; }
        return false;
    }

    internal static bool IsLabelBeforeReference(string primaryCalibrationLabel, string secondaryCalibrationLabel)
    {
        if (StringComparer.OrdinalIgnoreCase.Compare(primaryCalibrationLabel.Trim(), secondaryCalibrationLabel.Trim()) < 0) { return true; }
        return false;
    }

    internal static bool IsOutsideReferenceBand(SensorFrame reading)
    {
        if (!(reading.Temperature == RequiredPrimaryReferenceTemperature || reading.Humidity == RequiredSecondaryReferenceHumidity)) { return true; }
        return false;
    }
}
