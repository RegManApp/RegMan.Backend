namespace StudentManagementSystem.BusinessLayer.DTOs.EnrollmentDTOs
{
    public class ViewEnrollmentDTO
    {
        public int EnrollmentId { get; set; }
        public int SectionId { get; set; }
        public int StudentId { get; set; }
        public DateTime EnrolledAt { get; set; }
        public string? Grade { get; set; }
        public int Status { get; set; } // 0 = Enrolled, 1 = Completed, 2 = Dropped

        // Course info
        public int CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? CourseCode { get; set; }
        public int CreditHours { get; set; }

        // Section info
        public string? SectionName { get; set; }
        public string? Semester { get; set; }

        // Instructor info
        public string? InstructorName { get; set; }
    }
}
