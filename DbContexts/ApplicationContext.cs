using Microsoft.EntityFrameworkCore;
using WebAPI.Models.Courses;
using WebAPI.Models.Students;
using WebAPI.Models.Users;
using WebAPI.Models.Teachers;  // <-- ДОБАВИТЬ ЭТУ СТРОКУ

namespace WebAPI.DbContexts
{
    public class ApplicationContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentProgress> StudentProgresses { get; set; }
        public DbSet<GradeRecord> GradeRecords { get; set; }
        public DbSet<Teacher> Teachers { get; set; }  // <-- ДОБАВИТЬ ЭТУ СТРОКУ

        public ApplicationContext()
        {
        }

        public ApplicationContext(DbContextOptions<ApplicationContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=Mark;Username=postgres; Password=DIMA1412");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связи Teacher -> Course (один ко многим)
            modelBuilder.Entity<Course>()
                .HasOne(c => c.Teacher)
                .WithMany(t => t.Courses)
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            // Настройка связи User -> Teacher (один к одному)
            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка связи User -> Student (один к одному)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Course)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Остальные настройки...
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Course)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudentProgress>()
                .HasOne(sp => sp.Student)
                .WithMany(s => s.StudentProgress)
                .HasForeignKey(sp => sp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GradeRecord>()
                .HasOne(gr => gr.StudentProgress)
                .WithMany(sp => sp.Grades)
                .HasForeignKey(gr => gr.StudentProgressId)
                .OnDelete(DeleteBehavior.Cascade);

            // Индексы
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Login)
                .IsUnique();

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.Email)
                .IsUnique();

            modelBuilder.Entity<Teacher>()
                .HasIndex(t => t.Email)
                .IsUnique();

            modelBuilder.Entity<StudentProgress>()
                .HasIndex(sp => sp.StudentId);

            modelBuilder.Entity<StudentProgress>()
                .HasIndex(sp => sp.Status);

            modelBuilder.Entity<GradeRecord>()
                .HasIndex(gr => gr.Date);
        }
    }
}