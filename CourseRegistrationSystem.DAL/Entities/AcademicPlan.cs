
public class AcademicPlan
{
    public string AcademicPlanId{get;set;} 
    public string MajorName{get;set;} 
    public int Credits{get;set;} 

    public List<Course> courses;
    

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
        return total;
    }
}



