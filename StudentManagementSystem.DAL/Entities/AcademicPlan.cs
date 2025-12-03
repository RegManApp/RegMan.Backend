namespace StudentManagementSystem.Entities
{
    public class AcademicPlan
    {
        [Key]
        public string AcademicPlanId { get; set; }
        public string MajorName { get; set; }
        public int Credits { get; set; }

        public ICollection<Course> Courses = new HashSet<Course>();

        //    public void AddCourse(Course x)
        //    {
        //        if (x != null)
        //            courses.Add(x);
        //    }
        //
        //    public int GetTotalCredits()
        //   {
        //      int total = 0;
        //
        //        foreach (var c in courses)
        //        {
        //            total += c.Creds;
        //        }
        //
    }
}





