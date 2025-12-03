
public class AcademicPlan
{
    public string AcademicPlanId ;
    public string majorname ;
    public int Creds ;

    private List<Course> courses;

    public void SetAcademicPlanId(string x)
     {
        AcademicPlanId = x;
     }

    public void Setmajorname(string x)
     {
        majorname = x;
     }

    public void SetCreds(int x)
     {
        Creds = x;
     }

    public AcademicPlanId GetPlan()
     {
      return AcademicPlanId;
     }

    public majorname GetMajorName()
     {
      return majorname;
     }

    public Creds GetCreds()
     {
      return Creds;
     }

    public AcademicPlan(string x, string y, int z)
    {
        AcademicPlanId = x;
        majorname = y;
        Creds = z;
        courses = new List<ICollection>();
    }

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

