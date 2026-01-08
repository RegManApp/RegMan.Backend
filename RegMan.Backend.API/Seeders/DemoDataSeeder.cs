using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.DTOs.DevTools;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.API.Seeders
{
    public static class DemoDataSeeder
    {
        // Keep these stable so frontend can 1-click switch.
        public const string DemoAdminEmail = "admin@demo.local";
        public const string DemoInstructorEmail = "instructor@demo.local";
        public const string DemoStudentEmail = "student@demo.local";

        public const string DemoPassword = "Demo123!";

        public static async Task<List<DemoUserInfoDto>> GetDemoUsersAsync(UserManager<BaseUser> userManager)
        {
            var emails = new[] { DemoAdminEmail, DemoInstructorEmail, DemoStudentEmail };
            var users = await userManager.Users
                .Where(u => emails.Contains(u.Email!))
                .ToListAsync();

            return users
                .OrderBy(u => u.Email)
                .Select(u => new DemoUserInfoDto
                {
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName,
                    Role = u.Role
                })
                .ToList();
        }

        public static async Task SeedAsync(
            AppDbContext context,
            UserManager<BaseUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Roles
            await RoleSeeder.SeedRolesAsync(roleManager);

            // Users + Profiles
            var adminUser = await EnsureUserAsync(userManager, DemoAdminEmail, "Demo Admin", "Admin", DemoPassword);
            await EnsureAdminProfileAsync(context, adminUser.Id);

            var instructorUser = await EnsureUserAsync(userManager, DemoInstructorEmail, "Demo Instructor", "Instructor", DemoPassword);
            var instructorProfileId = await EnsureInstructorProfileAsync(context, instructorUser.Id);

            var studentUser = await EnsureUserAsync(userManager, DemoStudentEmail, "Demo Student", "Student", DemoPassword);
            var studentProfileId = await EnsureStudentProfileAndCartAsync(context, studentUser.Id);

            // Domain demo data (idempotent-ish)
            await EnsureCoreSchedulingDataAsync(context, instructorProfileId);
            await EnsureStudentAcademicDataAsync(context, studentProfileId);
            await EnsureOfficeHoursDataAsync(context, instructorProfileId, studentProfileId);
            await EnsureChatDemoAsync(context, instructorUser.Id, studentUser.Id);
            await EnsureNotificationsDemoAsync(context, studentUser.Id);

            await context.SaveChangesAsync();
        }

        private static async Task<BaseUser> EnsureUserAsync(
            UserManager<BaseUser> userManager,
            string email,
            string fullName,
            string role,
            string password)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user != null)
                return user;

            user = new BaseUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Address = "Demo Address",
                Role = role
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create demo user '{email}': {errors}");
            }

            // Ensure role claim exists
            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        private static async Task EnsureAdminProfileAsync(AppDbContext context, string userId)
        {
            var exists = await context.Admins.AnyAsync(a => a.UserId == userId);
            if (exists)
                return;

            context.Admins.Add(new AdminProfile
            {
                UserId = userId,
                Title = "Registrar"
            });

            await context.SaveChangesAsync();
        }

        private static async Task<int> EnsureInstructorProfileAsync(AppDbContext context, string userId)
        {
            var existing = await context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
            if (existing != null)
                return existing.InstructorId;

            var profile = new InstructorProfile
            {
                UserId = userId,
                Title = "Professor",
                Degree = InstructorDegree.Professor,
                Department = "Computer Science"
            };

            context.Instructors.Add(profile);
            await context.SaveChangesAsync();
            return profile.InstructorId;
        }

        private static async Task<int> EnsureStudentProfileAndCartAsync(AppDbContext context, string userId)
        {
            var existing = await context.Students
                .Include(s => s.Cart)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existing != null)
                return existing.StudentId;

            var defaultPlan = await context.AcademicPlans.AsNoTracking().FirstOrDefaultAsync();
            var planId = defaultPlan?.AcademicPlanId ?? "default";

            var student = new StudentProfile
            {
                UserId = userId,
                FamilyContact = "Demo Contact",
                CompletedCredits = 30,
                RegisteredCredits = 12,
                GPA = 3.2,
                AcademicPlanId = planId
            };

            context.Students.Add(student);
            await context.SaveChangesAsync();

            var cart = new Cart
            {
                StudentProfileId = student.StudentId
            };

            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Keep StudentProfile.CartId consistent for code that expects it.
            student.CartId = cart.CartId;
            await context.SaveChangesAsync();

            return student.StudentId;
        }

        private static async Task EnsureCoreSchedulingDataAsync(AppDbContext context, int instructorId)
        {
            // Rooms
            var roomA = await context.Rooms.FirstOrDefaultAsync(r => r.Building == "A" && r.RoomNumber == "101");
            if (roomA == null)
            {
                roomA = new Room { Building = "A", RoomNumber = "101", Capacity = 60, Type = RoomType.LectureHall };
                context.Rooms.Add(roomA);
            }

            var roomB = await context.Rooms.FirstOrDefaultAsync(r => r.Building == "B" && r.RoomNumber == "202");
            if (roomB == null)
            {
                roomB = new Room { Building = "B", RoomNumber = "202", Capacity = 35, Type = RoomType.Lab };
                context.Rooms.Add(roomB);
            }

            await context.SaveChangesAsync();

            // Courses
            var courses = new (string Code, string Name, int Credits, CourseCategory Cat)[]
            {
                ("ITCS101", "Intro to Computing", 3, CourseCategory.ITCS),
                ("ITCS201", "Data Structures", 3, CourseCategory.ITCS),
                ("ITCS202", "Discrete Math", 3, CourseCategory.ITCS),
                ("ITCS301", "Databases", 3, CourseCategory.ITCS),
                ("ITCS302", "Operating Systems", 3, CourseCategory.ITCS),
                ("ENG101", "Academic Writing", 3, CourseCategory.ENG),
                ("BA101", "Intro to Business", 3, CourseCategory.BA),
                ("BT101", "Intro to Biotechnology", 3, CourseCategory.BT),
            };

            foreach (var c in courses)
            {
                var exists = await context.Courses.AnyAsync(x => x.CourseCode == c.Code);
                if (!exists)
                {
                    context.Courses.Add(new Course
                    {
                        CourseCode = c.Code,
                        CourseName = c.Name,
                        CreditHours = c.Credits,
                        CourseCategory = c.Cat,
                        Description = "Demo course"
                    });
                }
            }

            await context.SaveChangesAsync();

            // Sections + Slots
            var demoSemester = "Fall";
            var demoYear = new DateTime(2025, 9, 1);

            var allCourses = await context.Courses.AsNoTracking().ToListAsync();
            var byCode = allCourses.ToDictionary(c => c.CourseCode);

            // Deterministic meeting patterns (no overlaps across courses for easy demo)
            var patterns = new (DayOfWeek[] Days, TimeSpan Start, TimeSpan End, Room Room, SlotType SlotType)[]
            {
                (new[]{ DayOfWeek.Sunday, DayOfWeek.Tuesday }, new TimeSpan(9,0,0), new TimeSpan(10,15,0), roomA, SlotType.Lecture),
                (new[]{ DayOfWeek.Sunday, DayOfWeek.Tuesday }, new TimeSpan(10,30,0), new TimeSpan(11,45,0), roomA, SlotType.Lecture),
                (new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday }, new TimeSpan(9,0,0), new TimeSpan(10,15,0), roomA, SlotType.Lecture),
                (new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday }, new TimeSpan(10,30,0), new TimeSpan(11,45,0), roomA, SlotType.Lecture),
                (new[]{ DayOfWeek.Thursday }, new TimeSpan(12,0,0), new TimeSpan(13,30,0), roomB, SlotType.Lab),
                (new[]{ DayOfWeek.Thursday }, new TimeSpan(13,45,0), new TimeSpan(15,15,0), roomB, SlotType.Lab),
            };

            var courseCodesToSeed = new[] { "ITCS101", "ITCS201", "ITCS202", "ITCS301", "ITCS302", "ENG101" };
            for (var i = 0; i < courseCodesToSeed.Length; i++)
            {
                var code = courseCodesToSeed[i];
                if (!byCode.TryGetValue(code, out var course))
                    continue;

                // Create two sections per course if missing
                for (var sectionIndex = 1; sectionIndex <= 2; sectionIndex++)
                {
                    var sectionName = $"{code}-S{sectionIndex}";
                    var existingSection = await context.Sections.FirstOrDefaultAsync(s =>
                        s.CourseId == course.CourseId &&
                        s.Semester == demoSemester &&
                        s.Year == demoYear &&
                        s.SectionName == sectionName);

                    if (existingSection == null)
                    {
                        existingSection = new Section
                        {
                            CourseId = course.CourseId,
                            Semester = demoSemester,
                            Year = demoYear,
                            SectionName = sectionName,
                            InstructorId = instructorId,
                            AvailableSeats = 30
                        };
                        context.Sections.Add(existingSection);
                        await context.SaveChangesAsync();
                    }

                    // Assign a deterministic pattern per section
                    var pattern = patterns[(i + (sectionIndex - 1)) % patterns.Length];

                    // Ensure TimeSlots and ScheduleSlots exist for each meeting day
                    foreach (var day in pattern.Days)
                    {
                        var timeSlot = await context.TimeSlots.FirstOrDefaultAsync(ts =>
                            ts.RoomId == pattern.Room.RoomId &&
                            ts.Day == day &&
                            ts.StartTime == pattern.Start &&
                            ts.EndTime == pattern.End);

                        if (timeSlot == null)
                        {
                            timeSlot = new TimeSlot
                            {
                                RoomId = pattern.Room.RoomId,
                                Day = day,
                                StartTime = pattern.Start,
                                EndTime = pattern.End
                            };
                            context.TimeSlots.Add(timeSlot);
                            await context.SaveChangesAsync();
                        }

                        var slotExists = await context.ScheduleSlots.AnyAsync(ss =>
                            ss.SectionId == existingSection.SectionId &&
                            ss.TimeSlotId == timeSlot.TimeSlotId);

                        if (!slotExists)
                        {
                            context.ScheduleSlots.Add(new ScheduleSlot
                            {
                                SectionId = existingSection.SectionId,
                                RoomId = pattern.Room.RoomId,
                                TimeSlotId = timeSlot.TimeSlotId,
                                InstructorId = instructorId,
                                SlotType = pattern.SlotType
                            });
                        }
                    }

                    await context.SaveChangesAsync();
                }
            }
        }

        private static async Task EnsureStudentAcademicDataAsync(AppDbContext context, int studentId)
        {
            // Transcript (2 completed courses)
            var hasAnyTranscript = await context.Transcripts.AnyAsync(t => t.StudentId == studentId);
            if (!hasAnyTranscript)
            {
                var ds = await context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseCode == "ITCS201");
                var eng = await context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseCode == "ENG101");

                if (ds != null)
                {
                    var section = await context.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.CourseId == ds.CourseId);
                    if (section != null)
                    {
                        context.Transcripts.Add(new Transcript
                        {
                            StudentId = studentId,
                            CourseId = ds.CourseId,
                            SectionId = section.SectionId,
                            Grade = "B+",
                            GradePoints = GradeHelper.GetGradePoints("B+"),
                            Semester = "Spring",
                            Year = 2024,
                            CreditHours = ds.CreditHours,
                            CompletedAt = DateTime.UtcNow.AddDays(-120)
                        });
                    }
                }

                if (eng != null)
                {
                    var section = await context.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.CourseId == eng.CourseId);
                    if (section != null)
                    {
                        context.Transcripts.Add(new Transcript
                        {
                            StudentId = studentId,
                            CourseId = eng.CourseId,
                            SectionId = section.SectionId,
                            Grade = "A-",
                            GradePoints = GradeHelper.GetGradePoints("A-"),
                            Semester = "Fall",
                            Year = 2023,
                            CreditHours = eng.CreditHours,
                            CompletedAt = DateTime.UtcNow.AddDays(-300)
                        });
                    }
                }

                await context.SaveChangesAsync();
            }

            // Enrollment: one Enrolled (occupied time) + one Pending (for approve/decline demo)
            var hasAnyEnrollment = await context.Enrollments.AnyAsync(e => e.StudentId == studentId);
            if (!hasAnyEnrollment)
            {
                var sectionEnrolled = await context.Sections
                    .AsNoTracking()
                    .OrderBy(s => s.SectionId)
                    .FirstOrDefaultAsync();

                if (sectionEnrolled != null)
                {
                    context.Enrollments.Add(new Enrollment
                    {
                        StudentId = studentId,
                        SectionId = sectionEnrolled.SectionId,
                        Status = Status.Enrolled,
                        EnrolledAt = DateTime.UtcNow.AddDays(-7)
                    });
                }

                var sectionPending = await context.Sections
                    .AsNoTracking()
                    .OrderByDescending(s => s.SectionId)
                    .FirstOrDefaultAsync();

                if (sectionPending != null)
                {
                    context.Enrollments.Add(new Enrollment
                    {
                        StudentId = studentId,
                        SectionId = sectionPending.SectionId,
                        Status = Status.Pending,
                        EnrolledAt = DateTime.UtcNow.AddDays(-1)
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureOfficeHoursDataAsync(AppDbContext context, int instructorId, int studentId)
        {
            // Create one office hour in the future
            var targetDate = DateTime.UtcNow.Date.AddDays(1);
            var officeHour = await context.OfficeHours.FirstOrDefaultAsync(o =>
                o.InstructorId == instructorId &&
                o.Date.Date == targetDate);

            if (officeHour == null)
            {
                var room = await context.Rooms.AsNoTracking().OrderBy(r => r.RoomId).FirstOrDefaultAsync();
                officeHour = new OfficeHour
                {
                    InstructorId = instructorId,
                    RoomId = room?.RoomId,
                    Date = targetDate,
                    StartTime = new TimeSpan(14, 0, 0),
                    EndTime = new TimeSpan(14, 30, 0),
                    Status = OfficeHourStatus.Available,
                    Notes = "Demo office hour"
                };
                context.OfficeHours.Add(officeHour);
                await context.SaveChangesAsync();
            }

            // Create a pending booking for that office hour
            var bookingExists = await context.OfficeHourBookings.AnyAsync(b =>
                b.OfficeHourId == officeHour.OfficeHourId &&
                b.StudentId == studentId);

            if (!bookingExists)
            {
                var studentProfile = await context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                if (studentProfile == null)
                    return;

                context.OfficeHourBookings.Add(new OfficeHourBooking
                {
                    OfficeHourId = officeHour.OfficeHourId,
                    BookerUserId = studentProfile.UserId,
                    BookerRole = "Student",
                    StudentId = studentId,
                    Status = BookingStatus.Pending,
                    Purpose = "Discuss course selection (demo)",
                    BookerNotes = "Looking for a compact schedule"
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureChatDemoAsync(AppDbContext context, string instructorUserId, string studentUserId)
        {
            // Create a conversation between the two users
            var existingConversationId = await context.ConversationParticipants
                .Where(cp => cp.UserId == instructorUserId || cp.UserId == studentUserId)
                .GroupBy(cp => cp.ConversationId)
                .Where(g => g.Select(x => x.UserId).Distinct().Count() == 2)
                .Select(g => g.Key)
                .FirstOrDefaultAsync();

            Conversation conversation;
            if (existingConversationId != 0)
            {
                conversation = await context.Conversations.FirstAsync(c => c.ConversationId == existingConversationId);
            }
            else
            {
                conversation = new Conversation
                {
                    ConversationName = "Demo Chat"
                };
                context.Conversations.Add(conversation);
                await context.SaveChangesAsync();

                context.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversation.ConversationId,
                    UserId = instructorUserId
                });
                context.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversation.ConversationId,
                    UserId = studentUserId
                });

                await context.SaveChangesAsync();
            }

            var hasMessage = await context.Messages.AnyAsync(m => m.ConversationId == conversation.ConversationId);
            if (!hasMessage)
            {
                context.Messages.Add(new Message
                {
                    ConversationId = conversation.ConversationId,
                    SenderId = instructorUserId,
                    SentAt = DateTime.UtcNow.AddMinutes(-30),
                    TextMessage = "Welcome! Ping me if you need help with registration (demo).",
                    Status = MsgStatus.Delivered
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureNotificationsDemoAsync(AppDbContext context, string studentUserId)
        {
            var exists = await context.Notifications.AnyAsync(n =>
                n.UserId == studentUserId &&
                n.Type == NotificationType.General &&
                n.Title == "Demo Ready");

            if (exists)
                return;

            context.Notifications.Add(new Notification
            {
                UserId = studentUserId,
                Type = NotificationType.General,
                Title = "Demo Ready",
                Message = "Your demo environment has been seeded. Try Smart Schedule and Notifications.",
                EntityType = null,
                EntityId = null,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }
}
