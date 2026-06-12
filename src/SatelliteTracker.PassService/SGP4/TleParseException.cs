namespace SatelliteTracker.PassService.SGP4;

public class TleParseException : Exception
{
    public TleParseException(string message) : base(message) { }
    public TleParseException(string message, Exception inner) : base(message, inner) { }
}
