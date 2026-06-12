namespace SatelliteTracker.PassService.SGP4;

internal static class Sgp4Constants
{
    public const double EarthRadiusKm = 6378.137;
    public const double GM = 398600.4418;           // km^3/s^2
    public const double J2 = 1.08262998905e-3;
    public const double J3 = -2.53215306e-6;
    public const double J4 = -1.61098761e-6;
    public const double Ke = 7.43669161e-2;         // sqrt(GM) in ER^3/2/min units
    public const double TwoPi = 2.0 * Math.PI;
    public const double MinutesPerDay = 1440.0;
    public const double SecondsPerDay = 86400.0;
    public const double AE = 1.0;                   // Earth radius in units
    public const double Xke = 0.0743669161;         // sqrt(GM) ER^(3/2)/min
    public const double Xj2 = 1.08262998905e-3;
    public const double Xj3 = -2.53215306e-6;
    public const double Xj4 = -1.61098761e-6;
    public const double Ck2 = 0.5 * Xj2 * AE * AE;
    public const double Ck4 = -0.375 * Xj4 * AE * AE * AE * AE;
    public const double Qoms2T = 1.88027916e-9;     // (q0-s)^4 ER^4
    public const double S = 1.01222928;             // s ER
    public const double EarthFlatFactor = 1.0 / 298.257223563;
    public const double EarthEccentricitySquared = 2 * EarthFlatFactor - EarthFlatFactor * EarthFlatFactor;
    public const double EarthRotationRate = 7.2921150e-5; // rad/s
    public const double J2000 = 2451545.0;          // Julian date of J2000 epoch
}
