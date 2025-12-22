using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SmartScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SmartScheduleController(AppDbContext context)
        {
            _context = context;
        }

        public class SmartScheduleRequestDTO
        {
            public List<int> CourseIds { get; set; } = new();

            // Optional: filter to a specific term
            public string? Semester { get; set; }
            public int? Year { get; set; }

            // Hard constraints
            public string? EarliestStart { get; set; } // "HH:mm"
            public string? LatestEnd { get; set; }     // "HH:mm"
            public List<DayOfWeek>? AvoidDays { get; set; }
            public bool IgnoreFullSections { get; set; } = true;

            // Preferences (soft)
            public bool PreferCompactSchedule { get; set; } = true;
            public List<DayOfWeek>? PreferredDaysOff { get; set; }

            // Safety bound for demo
            public int MaxSectionsPerCourse { get; set; } = 8;
        }

        public class SmartScheduleResponseDTO
        {
            public List<RecommendedSectionDTO> RecommendedSections { get; set; } = new();
            public List<UnscheduledCourseDTO> UnscheduledCourses { get; set; } = new();
            public ScheduleMetricsDTO Metrics { get; set; } = new();
            public List<string> Explanation { get; set; } = new();
        }

        public class ScheduleMetricsDTO
        {
            public int CoursesRequested { get; set; }
            public int CoursesScheduled { get; set; }
            public int DistinctClassDays { get; set; }
            public int TotalGapMinutes { get; set; }
            public string? EarliestClassStart { get; set; }
            public string? LatestClassEnd { get; set; }
        }

        public class UnscheduledCourseDTO
        {
            public int CourseId { get; set; }
            public string CourseName { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        public class RecommendedSectionDTO
        {
            public int CourseId { get; set; }
            public string CourseCode { get; set; } = string.Empty;
            public string CourseName { get; set; } = string.Empty;
            public int SectionId { get; set; }
            public string SectionName { get; set; } = string.Empty;
            public int AvailableSeats { get; set; }
            public string? InstructorName { get; set; }
            public List<SlotDTO> Slots { get; set; } = new();
        }

        public class SlotDTO
        {
            public DayOfWeek Day { get; set; }
            public string Start { get; set; } = string.Empty; // HH:mm
            public string End { get; set; } = string.Empty;   // HH:mm
            public string? RoomName { get; set; }
            public string SlotType { get; set; } = string.Empty;
        }

        private class Interval
        {
            public DayOfWeek Day { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public string Label { get; set; } = string.Empty;
        }

        [HttpPost("recommend")]
        [Authorize(Roles = "Student,Admin")]
        public async Task<IActionResult> Recommend([FromBody] SmartScheduleRequestDTO request)
        {
            if (request.CourseIds == null || request.CourseIds.Count == 0)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Please select at least one course.",
                    StatusCodes.Status400BadRequest));
            }

            var courseIds = request.CourseIds.Distinct().ToList();
            if (courseIds.Count > 8)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Please select up to 8 courses for Smart Builder (demo-safe limit).",
                    StatusCodes.Status400BadRequest));
            }

            if (!TryParseTime(request.EarliestStart, out var earliestStart))
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "EarliestStart must be in HH:mm format.",
                    StatusCodes.Status400BadRequest));
            }

            if (!TryParseTime(request.LatestEnd, out var latestEnd))
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "LatestEnd must be in HH:mm format.",
                    StatusCodes.Status400BadRequest));
            }

            var avoidDays = new HashSet<DayOfWeek>(request.AvoidDays ?? new List<DayOfWeek>());
            var preferredDaysOff = new HashSet<DayOfWeek>(request.PreferredDaysOff ?? new List<DayOfWeek>());

            var courses = await _context.Courses
                .Where(c => courseIds.Contains(c.CourseId))
                .AsNoTracking()
                .ToListAsync();

            // Gather candidates in one go
            var sectionsQuery = _context.Sections
                .Where(s => courseIds.Contains(s.CourseId));

            if (!string.IsNullOrWhiteSpace(request.Semester))
            {
                sectionsQuery = sectionsQuery.Where(s => s.Semester == request.Semester);
            }

            if (request.Year.HasValue)
            {
                // Section.Year is DateTime in this model; compare by year component
                sectionsQuery = sectionsQuery.Where(s => s.Year.Year == request.Year.Value);
            }

            if (request.IgnoreFullSections)
            {
                sectionsQuery = sectionsQuery.Where(s => s.AvailableSeats > 0);
            }

            var candidateSections = await sectionsQuery
                .Include(s => s.Course)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.TimeSlot)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.Room)
                .Include(s => s.Instructor)
                    .ThenInclude(i => i!.User)
                .AsNoTracking()
                .ToListAsync();

            // Filter candidates that violate hard constraints
            var candidatesByCourse = candidateSections
                .GroupBy(s => s.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Where(s => s.Slots != null && s.Slots.Count > 0)
                        .Where(s => SatisfiesHardConstraints(s, earliestStart, latestEnd, avoidDays))
                        .OrderBy(s => s.SectionId)
                        .Take(Math.Max(1, request.MaxSectionsPerCourse))
                        .ToList()
                );

            // Compute fixed occupied intervals from existing active enrollments
            var occupied = await GetStudentOccupiedIntervalsAsync();

            var response = new SmartScheduleResponseDTO();
            response.Explanation.Add($"Smart Builder: requested {courseIds.Count} course(s). Deterministic search + scoring.");

            var orderedCourses = courseIds
                .Select(cid => new
                {
                    CourseId = cid,
                    CandidateCount = candidatesByCourse.TryGetValue(cid, out var list) ? list.Count : 0
                })
                .OrderBy(x => x.CandidateCount)
                .ThenBy(x => x.CourseId)
                .ToList();

            var missingCandidates = orderedCourses.Where(x => x.CandidateCount == 0).ToList();
            foreach (var miss in missingCandidates)
            {
                var courseName = courses.FirstOrDefault(c => c.CourseId == miss.CourseId)?.CourseName ?? $"Course #{miss.CourseId}";
                response.UnscheduledCourses.Add(new UnscheduledCourseDTO
                {
                    CourseId = miss.CourseId,
                    CourseName = courseName,
                    Reason = "No available sections match the constraints (or no schedule slots exist)."
                });
            }

            var schedulableCourseIds = orderedCourses.Where(x => x.CandidateCount > 0).Select(x => x.CourseId).ToList();

            List<Section> bestSelection = new();
            int bestScore = int.MaxValue;

            void Search(int index, List<Section> chosen, List<Interval> chosenIntervals)
            {
                if (index >= schedulableCourseIds.Count)
                {
                    var score = ScoreSchedule(chosenIntervals, request.PreferCompactSchedule, preferredDaysOff);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestSelection = chosen.ToList();
                    }
                    return;
                }

                var courseId = schedulableCourseIds[index];
                var candidates = candidatesByCourse[courseId];

                foreach (var sec in candidates)
                {
                    var intervals = GetIntervals(sec);

                    // Conflict check vs fixed occupied and already chosen
                    if (HasAnyConflict(intervals, occupied) || HasAnyConflict(intervals, chosenIntervals))
                    {
                        continue;
                    }

                    // Soft pruning: if partial schedule already uses a preferred-day-off, keep going but it may score worse.
                    var newChosen = new List<Section>(chosen) { sec };
                    var newIntervals = new List<Interval>(chosenIntervals);
                    newIntervals.AddRange(intervals);

                    Search(index + 1, newChosen, newIntervals);
                }
            }

            Search(0, new List<Section>(), new List<Interval>());

            // Build response
            var selectedSectionIds = bestSelection.Select(s => s.SectionId).ToHashSet();

            foreach (var cid in schedulableCourseIds)
            {
                if (!selectedSectionIds.Any(id => bestSelection.Any(s => s.CourseId == cid)))
                {
                    var courseName = courses.FirstOrDefault(c => c.CourseId == cid)?.CourseName ?? $"Course #{cid}";
                    response.UnscheduledCourses.Add(new UnscheduledCourseDTO
                    {
                        CourseId = cid,
                        CourseName = courseName,
                        Reason = "All available sections conflict with other choices or your current enrolled timetable."
                    });
                }
            }

            foreach (var sec in bestSelection.OrderBy(s => s.CourseId).ThenBy(s => s.SectionId))
            {
                response.RecommendedSections.Add(ToRecommended(sec));
            }

            var allIntervals = bestSelection.SelectMany(GetIntervals).ToList();
            var metrics = ComputeMetrics(allIntervals);
            response.Metrics = new ScheduleMetricsDTO
            {
                CoursesRequested = courseIds.Count,
                CoursesScheduled = bestSelection.Count,
                DistinctClassDays = metrics.DistinctDays,
                TotalGapMinutes = metrics.TotalGapMinutes,
                EarliestClassStart = metrics.Earliest?.ToString(@"hh\:mm"),
                LatestClassEnd = metrics.Latest?.ToString(@"hh\:mm")
            };

            response.Explanation.Add($"Scheduled {response.Metrics.CoursesScheduled}/{response.Metrics.CoursesRequested} course(s). Days used: {response.Metrics.DistinctClassDays}. Total gaps: {response.Metrics.TotalGapMinutes} min.");

            if (preferredDaysOff.Count > 0)
            {
                response.Explanation.Add($"Preference: keep these day(s) free when possible: {string.Join(", ", preferredDaysOff)}.");
            }

            if (earliestStart.HasValue)
            {
                response.Explanation.Add($"Constraint: no classes before {earliestStart.Value.ToString(@"hh\:mm")}.");
            }
            if (latestEnd.HasValue)
            {
                response.Explanation.Add($"Constraint: no classes after {latestEnd.Value.ToString(@"hh\:mm")}.");
            }
            if (avoidDays.Count > 0)
            {
                response.Explanation.Add($"Constraint: avoid {string.Join(", ", avoidDays)}.");
            }

            return Ok(ApiResponse<object>.SuccessResponse(response));
        }

        private async Task<List<Interval>> GetStudentOccupiedIntervalsAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Interval>();
            }

            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null)
            {
                return new List<Interval>();
            }

            var enrollments = await _context.Enrollments
                .Where(e => e.StudentId == student.StudentId && (e.Status == Status.Enrolled || e.Status == Status.Pending))
                .Select(e => e.SectionId)
                .ToListAsync();

            if (enrollments.Count == 0)
            {
                return new List<Interval>();
            }

            var sections = await _context.Sections
                .Where(s => enrollments.Contains(s.SectionId))
                .Include(s => s.Course)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.TimeSlot)
                .AsNoTracking()
                .ToListAsync();

            return sections.SelectMany(GetIntervals).ToList();
        }

        private static bool TryParseTime(string? value, out TimeSpan? parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (TimeSpan.TryParse(value, out var ts))
            {
                parsed = ts;
                return true;
            }
            return false;
        }

        private static bool SatisfiesHardConstraints(Section section, TimeSpan? earliestStart, TimeSpan? latestEnd, HashSet<DayOfWeek> avoidDays)
        {
            foreach (var slot in section.Slots)
            {
                var ts = slot.TimeSlot;
                if (avoidDays.Contains(ts.Day)) return false;
                if (earliestStart.HasValue && ts.StartTime < earliestStart.Value) return false;
                if (latestEnd.HasValue && ts.EndTime > latestEnd.Value) return false;
                if (ts.EndTime <= ts.StartTime) return false;
            }
            return true;
        }

        private static List<Interval> GetIntervals(Section section)
        {
            var courseLabel = section.Course?.CourseCode ?? section.Course?.CourseName ?? $"Course {section.CourseId}";
            return section.Slots
                .Select(sl => new Interval
                {
                    Day = sl.TimeSlot.Day,
                    Start = sl.TimeSlot.StartTime,
                    End = sl.TimeSlot.EndTime,
                    Label = $"{courseLabel} (Section {section.SectionName ?? section.SectionId.ToString()})"
                })
                .ToList();
        }

        private static bool HasAnyConflict(List<Interval> a, List<Interval> b)
        {
            foreach (var i in a)
            {
                foreach (var j in b)
                {
                    if (i.Day != j.Day) continue;
                    if (i.Start < j.End && i.End > j.Start) return true;
                }
            }
            return false;
        }

        private static int ScoreSchedule(List<Interval> intervals, bool preferCompact, HashSet<DayOfWeek> preferredDaysOff)
        {
            if (intervals.Count == 0) return int.MaxValue - 1;

            var byDay = intervals
                .GroupBy(i => i.Day)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var daysUsed = byDay.Count;
            var totalGapMinutes = 0;

            foreach (var day in byDay.Keys)
            {
                var list = byDay[day];
                for (int idx = 1; idx < list.Count; idx++)
                {
                    var gap = list[idx].Start - list[idx - 1].End;
                    if (gap > TimeSpan.Zero)
                    {
                        totalGapMinutes += (int)gap.TotalMinutes;
                    }
                }
            }

            var dayPenalty = daysUsed * 100;
            var gapWeight = preferCompact ? 2 : 1;
            var gapPenalty = totalGapMinutes * gapWeight;

            var preferredDaysPenalty = 0;
            foreach (var d in byDay.Keys)
            {
                if (preferredDaysOff.Contains(d))
                {
                    preferredDaysPenalty += 250;
                }
            }

            return dayPenalty + gapPenalty + preferredDaysPenalty;
        }

        private static (int DistinctDays, int TotalGapMinutes, TimeSpan? Earliest, TimeSpan? Latest) ComputeMetrics(List<Interval> intervals)
        {
            if (intervals.Count == 0) return (0, 0, null, null);

            var earliest = intervals.Min(i => i.Start);
            var latest = intervals.Max(i => i.End);

            var byDay = intervals
                .GroupBy(i => i.Day)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var totalGapMinutes = 0;
            foreach (var day in byDay.Keys)
            {
                var list = byDay[day];
                for (int idx = 1; idx < list.Count; idx++)
                {
                    var gap = list[idx].Start - list[idx - 1].End;
                    if (gap > TimeSpan.Zero)
                    {
                        totalGapMinutes += (int)gap.TotalMinutes;
                    }
                }
            }

            return (byDay.Count, totalGapMinutes, earliest, latest);
        }

        private static RecommendedSectionDTO ToRecommended(Section sec)
        {
            return new RecommendedSectionDTO
            {
                CourseId = sec.CourseId,
                CourseCode = sec.Course?.CourseCode ?? string.Empty,
                CourseName = sec.Course?.CourseName ?? string.Empty,
                SectionId = sec.SectionId,
                SectionName = sec.SectionName ?? $"Section {sec.SectionId}",
                AvailableSeats = sec.AvailableSeats,
                InstructorName = sec.Instructor?.User?.FullName,
                Slots = sec.Slots
                    .OrderBy(s => s.TimeSlot.Day)
                    .ThenBy(s => s.TimeSlot.StartTime)
                    .Select(s => new SlotDTO
                    {
                        Day = s.TimeSlot.Day,
                        Start = s.TimeSlot.StartTime.ToString(@"hh\:mm"),
                        End = s.TimeSlot.EndTime.ToString(@"hh\:mm"),
                        RoomName = s.Room == null ? null : $"{s.Room.Building} {s.Room.RoomNumber}",
                        SlotType = s.SlotType.ToString(),
                    })
                    .ToList()
            };
        }
    }
}
