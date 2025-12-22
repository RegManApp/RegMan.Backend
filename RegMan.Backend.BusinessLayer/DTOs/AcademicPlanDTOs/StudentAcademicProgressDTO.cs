namespace RegMan.Backend.BusinessLayer.DTOs.AcademicPlanDTOs
{
    public class StudentAcademicProgressDTO
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string AcademicPlanId { get; set; } = string.Empty;
        public string MajorName { get; set; } = string.Empty;
        public int TotalCreditsRequired { get; set; }
        public int CreditsCompleted { get; set; }
        public int CreditsRemaining { get; set; }
        public double ProgressPercentage { get; set; }
        public double CurrentGPA { get; set; }
        public int ExpectedGraduationYear { get; set; }

        // Course completion summary
        public int RequiredCoursesCount { get; set; }
        public int RequiredCoursesCompletedCount { get; set; }
        public int TotalPlanCoursesCount { get; set; }
        public int PlanCoursesCompletedCount { get; set; }

        // Warnings are computed using plan ordering as a proxy for prerequisites.
        // (There is no explicit prerequisites table in this codebase.)
        public IEnumerable<PrerequisiteWarningDTO> MissingPrerequisiteWarnings { get; set; } = new List<PrerequisiteWarningDTO>();

        public IEnumerable<CourseProgressDTO> CompletedCourses { get; set; } = new List<CourseProgressDTO>();
        public IEnumerable<CourseProgressDTO> InProgressCourses { get; set; } = new List<CourseProgressDTO>();
        public IEnumerable<CourseProgressDTO> RemainingCourses { get; set; } = new List<CourseProgressDTO>();
    }

    public class PrerequisiteWarningDTO
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public IEnumerable<string> MissingCourseCodes { get; set; } = new List<string>();
    }

    public class CourseProgressDTO
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int CreditHours { get; set; }
        public int RecommendedSemester { get; set; }
        public int RecommendedYear { get; set; }
        public bool IsRequired { get; set; }
        public string CourseType { get; set; } = string.Empty;
        public string? Grade { get; set; }
        public string Status { get; set; } = string.Empty; // Completed, InProgress, NotStarted
    }
}
