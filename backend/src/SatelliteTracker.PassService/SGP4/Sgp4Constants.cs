namespace SatelliteTracker.PassService.SGP4;

// this class contains constants used in the SGP4 algorithm
internal static class Sgp4Constants
{
    public const double EarthRadiusKm = 6378.137;    // Earth's equatorial radius in kilometers
    public const double GM = 398600.4418;           // km^3/s^2
    public const double J2 = 1.08262998905e-3;     // Earth's second zonal harmonic
    public const double J3 = -2.53215306e-6;        // Earth's third zonal harmonic
    public const double J4 = -1.61098761e-6;        // Earth's fourth zonal harmonic
    public const double Ke = 7.43669161e-2;         // sqrt(GM) in ER^3/2/min units
    public const double TwoPi = 2.0 * Math.PI;      // 2 * pi
    public const double MinutesPerDay = 1440.0;     // Number of minutes in a day
    public const double SecondsPerDay = 86400.0;    // Number of seconds in a day
    public const double AE = 1.0;                   // Earth radius in units
    public const double Xke = 0.0743669161;         // sqrt(GM) ER^(3/2)/min
    public const double Xj2 = 1.08262998905e-3;     // J2 coefficient
    public const double Xj3 = -2.53215306e-6;       // J3 coefficient
    public const double Xj4 = -1.61098761e-6;       // J4 coefficient
    public const double Ck2 = 0.5 * Xj2 * AE * AE;   // Ck2 = 0.5 * J2 * (EarthRadiusKm / AE)^2
    public const double Ck4 = -0.375 * Xj4 * AE * AE * AE * AE;   // Ck4 = -0.375 * J4 * (EarthRadiusKm / AE)^4
    public const double Qoms2T = 1.88027916e-9;     // (q0-s)^4 ER^4
    public const double S = 1.01222928;             // s ER
    public const double EarthFlatFactor = 1.0 / 298.257223563;   // Earth's flattening factor
    public const double EarthEccentricitySquared = 2 * EarthFlatFactor - EarthFlatFactor * EarthFlatFactor; // Earth's eccentricity squared
    public const double EarthRotationRate = 7.2921150e-5; // Earth's rotation rate in rad/s
    public const double J2000 = 2451545.0;          // Julian date of J2000 epoch
}
