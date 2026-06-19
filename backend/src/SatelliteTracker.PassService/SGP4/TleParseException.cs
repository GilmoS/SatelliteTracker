namespace SatelliteTracker.PassService.SGP4;

// this exception is thrown when there is an error parsing a TLE string, such as invalid format or missing fields
public class TleParseException : Exception
{
    public TleParseException(string message) : base(message) { }

    public TleParseException(string message, Exception inner) : base(message, inner) { }
}
