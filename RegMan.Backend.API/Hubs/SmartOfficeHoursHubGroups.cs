namespace RegMan.Backend.API.Hubs
{
    public static class SmartOfficeHoursHubGroups
    {
        public static string Students(int officeHourId) => $"officehours:{officeHourId}:students";
        public static string Providers(int officeHourId) => $"officehours:{officeHourId}:providers";
    }
}
