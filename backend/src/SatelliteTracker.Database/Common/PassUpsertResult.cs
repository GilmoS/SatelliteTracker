namespace SatelliteTracker.Database.Common;

// What IPassRepository.UpsertUpcomingAsync did for one satellite. SkippedPast counts computed
// passes whose orbit already belongs to a past/in-progress row, which the upsert never touches.
public record PassUpsertResult(int Inserted, int Updated, int Deleted, int SkippedPast);
