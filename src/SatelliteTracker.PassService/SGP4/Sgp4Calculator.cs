namespace SatelliteTracker.PassService.SGP4;

public static class Sgp4Calculator
{
    private const double Rad = Math.PI / 180.0;
    private const double Deg = 180.0 / Math.PI;
    private const double TwoPi = 2.0 * Math.PI;
    private const double EarthRadiusKm = 6378.137;
    private const double XKE = 0.07436691613317;    // sqrt(GM) in ER^(3/2)/min
    private const double XJ2 = 1.08262998905e-3;
    private const double XJ3 = -2.53215306e-6;
    private const double XJ4 = -1.61098761e-6;
    private const double CK2 = 0.5 * XJ2;
    private const double CK4 = -0.375 * XJ4;
    private const double S = 1.01222928;
    private const double QOMS2T = 1.88027916e-9;
    private const double MinutesPerDay = 1440.0;

    public static PositionVelocity CalculatePositionVelocity(TleData tle, DateTime utcTime)
    {
        double tsince = (utcTime - tle.Epoch).TotalMinutes;
        return RunSgp4(tle, tsince);
    }

    public static GeoPosition ToGeodetic(PositionVelocity pv, DateTime utcTime)
    {
        double thetaGst = GreenwichSiderealTime(utcTime);

        double x = pv.X, y = pv.Y, z = pv.Z;
        double lon = Math.Atan2(y, x) - thetaGst;
        lon = NormalizeAngle(lon);

        double r = Math.Sqrt(x * x + y * y);
        double lat = Math.Atan2(z, r);

        // Iterative geodetic latitude
        const double e2 = Sgp4Constants.EarthEccentricitySquared;
        for (int i = 0; i < 10; i++)
        {
            double sinLat = Math.Sin(lat);
            double n = EarthRadiusKm / Math.Sqrt(1 - e2 * sinLat * sinLat);
            double latNew = Math.Atan2(z + e2 * n * sinLat, r);
            if (Math.Abs(latNew - lat) < 1e-12) break;
            lat = latNew;
        }

        double sinLat2 = Math.Sin(lat);
        double nFinal = EarthRadiusKm / Math.Sqrt(1 - e2 * sinLat2 * sinLat2);
        double altKm = r / Math.Cos(lat) - nFinal;

        return new GeoPosition(lat * Deg, lon * Deg, altKm);
    }

    public static ObserverPosition CalculateObserverPosition(double latDeg, double lngDeg, double altMeters)
    {
        double lat = latDeg * Rad;
        double lng = lngDeg * Rad;
        double alt = altMeters / 1000.0;

        const double e2 = Sgp4Constants.EarthEccentricitySquared;
        double sinLat = Math.Sin(lat);
        double cosLat = Math.Cos(lat);
        double n = EarthRadiusKm / Math.Sqrt(1 - e2 * sinLat * sinLat);

        double x = (n + alt) * cosLat * Math.Cos(lng);
        double y = (n + alt) * cosLat * Math.Sin(lng);
        double z = (n * (1 - e2) + alt) * sinLat;

        return new ObserverPosition(x, y, z);
    }

    public static LookAngles CalculateLookAngles(PositionVelocity satPv, ObserverPosition obs, DateTime utcTime)
    {
        double thetaGst = GreenwichSiderealTime(utcTime);

        double obsLng = Math.Atan2(obs.Y, obs.X);
        double obsLat = Math.Atan2(obs.Z,
            Math.Sqrt(obs.X * obs.X + obs.Y * obs.Y) * (1 - Sgp4Constants.EarthEccentricitySquared));

        // Range vector in ECI
        double rx = satPv.X - obs.X;
        double ry = satPv.Y - obs.Y;
        double rz = satPv.Z - obs.Z;
        double range = Math.Sqrt(rx * rx + ry * ry + rz * rz);

        // Convert to SEZ (South-East-Zenith) topocentric
        double sinLat = Math.Sin(obsLat);
        double cosLat = Math.Cos(obsLat);
        double sinLng = Math.Sin(obsLng + thetaGst);
        double cosLng = Math.Cos(obsLng + thetaGst);

        // Rotate from ECI to SEZ
        double south  = sinLat * cosLng * rx + sinLat * sinLng * ry - cosLat * rz;
        double east   = -sinLng * rx + cosLng * ry;
        double zenith =  cosLat * cosLng * rx + cosLat * sinLng * ry + sinLat * rz;

        double elevation = Math.Asin(zenith / range) * Deg;
        double azimuth = Math.Atan2(-east, south) * Deg;
        if (azimuth < 0) azimuth += 360.0;

        return new LookAngles(azimuth, elevation, range);
    }

    // --- Core SGP4 propagator (simplified/near-Earth orbit) ---

    private static PositionVelocity RunSgp4(TleData tle, double tsince)
    {
        double xno  = tle.MeanMotion * TwoPi / MinutesPerDay;  // rad/min
        double xnodeo = tle.RightAscension * Rad;
        double omegao = tle.ArgumentOfPerigee * Rad;
        double xmo   = tle.MeanAnomaly * Rad;
        double xincl = tle.Inclination * Rad;
        double eo    = tle.Eccentricity;
        double bstar = tle.BStarDrag;

        double a1 = Math.Pow(XKE / xno, 2.0 / 3.0);
        double cosio = Math.Cos(xincl);
        double theta2 = cosio * cosio;
        double x3thm1 = 3.0 * theta2 - 1.0;
        double eosq = eo * eo;
        double betao2 = 1.0 - eosq;
        double betao = Math.Sqrt(betao2);

        double del1 = 1.5 * CK2 * x3thm1 / (a1 * a1 * betao * betao2);
        double ao   = a1 * (1.0 - del1 * (0.5 / 3.0 + del1 * (1.0 + 134.0 / 81.0 * del1)));
        double delo = 1.5 * CK2 * x3thm1 / (ao * ao * betao * betao2);
        double xnodp = xno / (1.0 + delo);
        double aodp = ao / (1.0 - delo);

        double s4 = S;
        double qoms24 = QOMS2T;
        double perigee = (aodp * (1.0 - eo) - 1.0) * EarthRadiusKm;

        if (perigee < 156.0)
        {
            s4 = perigee - 78.0;
            if (perigee <= 98.0) s4 = 20.0;
            qoms24 = Math.Pow((120.0 - s4) / EarthRadiusKm, 4.0);
            s4 = s4 / EarthRadiusKm + 1.0;
        }

        double pinvsq = 1.0 / (aodp * aodp * betao2 * betao2);
        double tsi = 1.0 / (aodp - s4);
        double eta = aodp * eo * tsi;
        double etasq = eta * eta;
        double eeta = eo * eta;
        double psisq = Math.Abs(1.0 - etasq);
        double coef = qoms24 * Math.Pow(tsi, 4.0);
        double coef1 = coef / Math.Pow(psisq, 3.5);

        double c2 = coef1 * xnodp
            * (aodp * (1.0 + 1.5 * etasq + eeta * (4.0 + etasq))
            + 0.75 * CK2 * tsi / psisq * x3thm1 * (8.0 + 3.0 * etasq * (8.0 + etasq)));
        double c1 = bstar * c2;

        double sinio = Math.Sin(xincl);
        double a3ovk2 = -XJ3 / CK2;
        double c3 = coef * tsi * a3ovk2 * xnodp * sinio / eo;
        double x1mth2 = 1.0 - theta2;

        double c4 = 2.0 * xnodp * coef1 * aodp * betao2
            * (eta * (2.0 + 0.5 * etasq) + eo * (0.5 + 2.0 * etasq)
            - 2.0 * CK2 * tsi / (aodp * psisq)
            * (-3.0 * x3thm1 * (1.0 - 2.0 * eeta + etasq * (1.5 - 0.5 * eeta))
            + 0.75 * x1mth2 * (2.0 * etasq - eeta * (1.0 + etasq))
            * Math.Cos(2.0 * omegao)));

        double c5 = 2.0 * coef1 * aodp * betao2
            * (1.0 + 2.75 * (etasq + eeta) + eeta * etasq);

        double theta4 = theta2 * theta2;
        double temp1 = 3.0 * CK2 * pinvsq * xnodp;
        double temp2 = temp1 * CK2 * pinvsq;
        double temp3 = 1.25 * CK4 * pinvsq * pinvsq * xnodp;

        double xmdot = xnodp
            + 0.5 * temp1 * betao * x3thm1
            + 0.0625 * temp2 * betao * (13.0 - 78.0 * theta2 + 137.0 * theta4);

        double x1m5th = 1.0 - 5.0 * theta2;
        double omgdot = -0.5 * temp1 * x1m5th
            + 0.0625 * temp2 * (7.0 - 114.0 * theta2 + 395.0 * theta4)
            + temp3 * (3.0 - 36.0 * theta2 + 49.0 * theta4);

        double xhdot1 = -temp1 * cosio;
        double xnodot = xhdot1
            + (0.5 * temp2 * (4.0 - 19.0 * theta2) + 2.0 * temp3 * (3.0 - 7.0 * theta2)) * cosio;

        double omgcof = bstar * c3 * Math.Cos(omegao);
        double xmcof = Math.Abs(eo) > 1e-4
            ? -2.0 / 3.0 * coef * bstar / eeta
            : 0.0;
        double xnodcf = 3.5 * betao2 * xhdot1 * c1;
        double t2cof = 1.5 * c1;

        double xlcof = 0.125 * a3ovk2 * sinio
            * (3.0 + 5.0 * cosio) / (1.0 + cosio);
        double aycof = 0.25 * a3ovk2 * sinio;

        double delmo = Math.Pow(1.0 + eta * Math.Cos(xmo), 3.0);
        double sinmo = Math.Sin(xmo);
        double x7thm1 = 7.0 * theta2 - 1.0;

        // Update for secular gravity and atmospheric drag
        double xmdf = xmo + xmdot * tsince;
        double omgadf = omegao + omgdot * tsince;
        double xnoddf = xnodeo + xnodot * tsince;

        double omega = omgadf;
        double xmp = xmdf;
        double tsq = tsince * tsince;
        double xnode = xnoddf + xnodcf * tsq;
        double tempa = 1.0 - c1 * tsince;
        double tempe = bstar * c4 * tsince;
        double templ = t2cof * tsq;

        xmp   += xmcof * (Math.Pow(1.0 + eta * Math.Cos(xmdf), 3.0) - delmo);
        omega += omgcof * tsince;
        tempe += omgcof * c5 * (Math.Sin(xmp) - sinmo);
        templ += tsince * tsq * (c1 * c1 / 2.0);

        double a = aodp * tempa * tempa;
        double e = eo - tempe;
        e = Math.Max(e, 1e-6);
        double xl = xmp + omega + xnode + xnodp * templ;

        double beta = Math.Sqrt(1.0 - e * e);
        double xn = XKE / Math.Pow(a, 1.5);

        // Long period periodics
        double axn = e * Math.Cos(omega);
        double temp = 1.0 / (a * beta * beta);
        double xll = temp * xlcof * axn;
        double aynl = temp * aycof;
        double xlt = xl + xll;
        double ayn = e * Math.Sin(omega) + aynl;

        // Solve Kepler's equation (Kepler's iteration)
        double u = Fmod2p(xlt - xnode);
        double eo1 = u;
        double ax = 0, ay = 0;
        for (int i = 0; i < 10; i++)
        {
            double sineo1 = Math.Sin(eo1);
            double coseo1 = Math.Cos(eo1);
            double tem5 = 1.0 - coseo1 * axn - sineo1 * ayn;
            double delta = (u - ayn * coseo1 + axn * sineo1 - eo1) / tem5;
            delta = Math.Max(-0.95, Math.Min(0.95, delta));
            eo1 += delta;
            if (Math.Abs(delta) < 1e-12) break;
            ax = coseo1;
            ay = sineo1;
        }

        // Short period preliminary quantities
        double ecose = axn * Math.Cos(eo1) + ayn * Math.Sin(eo1);
        double esine = axn * Math.Sin(eo1) - ayn * Math.Cos(eo1);
        double el2 = axn * axn + ayn * ayn;
        double pl = a * (1.0 - el2);
        double r = a * (1.0 - ecose);
        double rdot = XKE * Math.Sqrt(a) * esine / r;
        double rfdot = XKE * Math.Sqrt(pl) / r;
        double temp2b = a / r;
        double cosu = temp2b * (Math.Cos(eo1) - axn + ayn * esine / (1.0 + Math.Sqrt(1.0 - el2)));
        double sinu = temp2b * (Math.Sin(eo1) - ayn - axn * esine / (1.0 + Math.Sqrt(1.0 - el2)));
        double su = Math.Atan2(sinu, cosu);
        double sin2u = 2.0 * sinu * cosu;
        double cos2u = 2.0 * cosu * cosu - 1.0;
        temp = 1.0 / pl;
        double temp1b = CK2 * temp;
        double temp2c = temp1b * temp;

        // Update for short period periodics
        double rk = r * (1.0 - 1.5 * temp2c * betao * x3thm1)
            + 0.5 * temp1b * x1mth2 * cos2u;
        double uk = su - 0.25 * temp2c * x7thm1 * sin2u;
        double xnodek = xnode + 1.5 * temp2c * cosio * sin2u;
        double xinck = xincl + 1.5 * temp2c * cosio * sinio * cos2u;
        double rdotk = rdot - xn * temp1b * x1mth2 * sin2u;
        double rfdotk = rfdot + xn * temp1b * (x1mth2 * cos2u + 1.5 * x3thm1);

        // Orientation vectors
        double sinuk = Math.Sin(uk);
        double cosuk = Math.Cos(uk);
        double sinik = Math.Sin(xinck);
        double cosik = Math.Cos(xinck);
        double sinnok = Math.Sin(xnodek);
        double cosnok = Math.Cos(xnodek);

        double xmx = -sinnok * cosik;
        double xmy = cosnok * cosik;
        double ux = xmx * sinuk + cosnok * cosuk;
        double uy = xmy * sinuk + sinnok * cosuk;
        double uz = sinik * sinuk;
        double vx = xmx * cosuk - cosnok * sinuk;
        double vy = xmy * cosuk - sinnok * sinuk;
        double vz = sinik * cosuk;

        // Position and velocity in km and km/s
        double posX = rk * ux * EarthRadiusKm;
        double posY = rk * uy * EarthRadiusKm;
        double posZ = rk * uz * EarthRadiusKm;
        double velX = (rdotk * ux + rfdotk * vx) * EarthRadiusKm / 60.0;
        double velY = (rdotk * uy + rfdotk * vy) * EarthRadiusKm / 60.0;
        double velZ = (rdotk * uz + rfdotk * vz) * EarthRadiusKm / 60.0;

        return new PositionVelocity(posX, posY, posZ, velX, velY, velZ);
    }

    public static double GreenwichSiderealTime(DateTime utcTime)
    {
        // Julian date
        double jd = ToJulianDate(utcTime);
        double t = (jd - 2451545.0) / 36525.0;

        // GMST in seconds at 0h UT1
        double theta = 67310.54841
            + (876600.0 * 3600.0 + 8640184.812866) * t
            + 0.093104 * t * t
            - 6.2e-6 * t * t * t;

        theta = theta * Math.PI / 43200.0; // convert seconds to radians
        return NormalizeAngle(theta);
    }

    private static double ToJulianDate(DateTime dt)
    {
        int y = dt.Year;
        int m = dt.Month;
        if (m <= 2) { y--; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716))
             + Math.Floor(30.6001 * (m + 1))
             + dt.Day + b - 1524.5
             + (dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0 + dt.Millisecond / 3600000.0) / 24.0;
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= TwoPi;
        if (angle < 0) angle += TwoPi;
        return angle;
    }

    private static double Fmod2p(double x)
    {
        x %= TwoPi;
        if (x < 0) x += TwoPi;
        return x;
    }
}
