using System;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace HWInventory.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("HWInventory.Domain.Entities.AppRole", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ConcurrencyStamp")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Description")
                    .HasMaxLength(400)
                    .HasColumnType("nvarchar(400)");

                b.Property<string>("Name")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("NormalizedName")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.HasKey("Id");

                b.HasIndex("NormalizedName").IsUnique().HasFilter("[NormalizedName] IS NOT NULL");

                b.ToTable("AspNetRoles", (string)null);
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.AppSetting", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Key")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Section")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Value")
                    .HasColumnType("nvarchar(max)");

                b.HasKey("Id");

                b.ToTable("AppSettings");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.BackupJob", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ArtifactPath")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("EmailRecipients")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<bool>("EncryptionEnabled")
                    .HasColumnType("bit");

                b.Property<string>("FailureReason")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<string>("FileName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<long?>("FileSizeBytes")
                    .HasColumnType("bigint");

                b.Property<bool?>("IntegrityPassed")
                    .HasColumnType("bit");

                b.Property<bool>("IntegrityCheckEnabled")
                    .HasColumnType("bit");

                b.Property<DateTime?>("IntegrityCheckedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<bool>("IsAutomatic")
                    .HasColumnType("bit");

                b.Property<int>("JobStatus")
                    .HasColumnType("int");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("ProtectedEncryptionSecret")
                    .HasColumnType("nvarchar(max)");

                b.Property<int>("Scope")
                    .HasColumnType("int");

                b.Property<bool>("SendEmail")
                    .HasColumnType("bit");

                b.Property<string>("StoragePath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.ToTable("BackupJobs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ExportJob", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ArtifactPath")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("EmailRecipients")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<DateTime?>("ExpiresAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("FailureReason")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<string>("FileName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<long?>("FileSizeBytes")
                    .HasColumnType("bigint");

                b.Property<string>("FilterJson")
                    .HasMaxLength(2048)
                    .HasColumnType("nvarchar(2048)");

                b.Property<int>("Format")
                    .HasColumnType("int");

                b.Property<int>("DownloadCount")
                    .HasColumnType("int");

                b.Property<int>("JobStatus")
                    .HasColumnType("int");

                b.Property<DateTime?>("LastDownloadedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasColumnType("nvarchar(max)");

                b.Property<int>("Scope")
                    .HasColumnType("int");

                b.Property<bool>("SendEmail")
                    .HasColumnType("bit");

                b.Property<string>("StoragePath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.ToTable("ExportJobs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ImportJob", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<int>("ConflictStrategy")
                    .HasColumnType("int");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<long?>("CreatedRows")
                    .HasColumnType("bigint");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<bool>("DryRun")
                    .HasColumnType("bit");

                b.Property<string>("EmailRecipients")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<string>("FailureReason")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("Format")
                    .HasColumnType("int");

                b.Property<int>("JobStatus")
                    .HasColumnType("int");

                b.Property<string>("MappingJson")
                    .HasMaxLength(2048)
                    .HasColumnType("nvarchar(2048)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("OriginalFileName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<long?>("ProcessedRows")
                    .HasColumnType("bigint");

                b.Property<string>("ResultLog")
                    .HasMaxLength(2048)
                    .HasColumnType("nvarchar(2048)");

                b.Property<long?>("SkippedRows")
                    .HasColumnType("bigint");

                b.Property<int>("Scope")
                    .HasColumnType("int");

                b.Property<bool>("SendEmail")
                    .HasColumnType("bit");

                b.Property<long?>("UpdatedRows")
                    .HasColumnType("bigint");

                b.Property<string>("StoragePath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<string>("StoredFilePath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.ToTable("ImportJobs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ReportDefinition", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Description")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<bool>("Enabled")
                    .HasColumnType("bit");

                b.Property<string>("FilterJson")
                    .HasMaxLength(4000)
                    .HasColumnType("nvarchar(4000)");

                b.Property<int>("Format")
                    .HasColumnType("int");

                b.Property<DateTimeOffset?>("LastRunAtUtc")
                    .HasColumnType("datetimeoffset");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTimeOffset?>("NextRunAtUtc")
                    .HasColumnType("datetimeoffset");

                b.Property<int>("Recurrence")
                    .HasColumnType("int");

                b.Property<string>("Recipients")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<TimeSpan?>("RunAtTime")
                    .HasColumnType("time");

                b.Property<int?>("RunOnDayOfMonth")
                    .HasColumnType("int");

                b.Property<int?>("RunOnDayOfWeek")
                    .HasColumnType("int");

                b.Property<int>("Scope")
                    .HasColumnType("int");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<string>("StoragePath")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.HasKey("Id");

                b.ToTable("ReportDefinitions");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ReportRun", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ArtifactPath")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTimeOffset?>("CompletedAtUtc")
                    .HasColumnType("datetimeoffset");

                b.Property<string>("DownloadToken")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("FailureReason")
                    .HasMaxLength(2000)
                    .HasColumnType("nvarchar(2000)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid>("ReportDefinitionId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTimeOffset>("StartedAtUtc")
                    .HasColumnType("datetimeoffset");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.HasIndex("ReportDefinitionId");

                b.ToTable("ReportRuns");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.UpdatePackage", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("BackupStoragePath")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<bool>("ConfirmedBackupAvailable")
                    .HasColumnType("bit");

                b.Property<bool>("CreateBackupBeforeInstall")
                    .HasColumnType("bit");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("FailureReason")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<string>("FileName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("LogPath")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<string>("ManifestJson")
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("Notes")
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<bool>("PerformIntegrityCheck")
                    .HasColumnType("bit");

                b.Property<bool>("PreserveDatabaseConfiguration")
                    .HasColumnType("bit");

                b.Property<string>("Sha256")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("nvarchar(128)");

                b.Property<string>("StagingPath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<string>("StoredPath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.Property<int>("UpdateStatus")
                    .HasColumnType("int");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.HasKey("Id");

                b.HasIndex("CreatedAtUtc");

                b.HasIndex("UpdateStatus");

                b.ToTable("UpdatePackages");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.BackupSchedule", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<int?>("DayOfMonth")
                    .HasColumnType("int");

                b.Property<int?>("DayOfWeek")
                    .HasColumnType("int");

                b.Property<string>("EmailRecipients")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<bool>("Enabled")
                    .HasColumnType("bit");

                b.Property<bool>("EncryptionEnabled")
                    .HasColumnType("bit");

                b.Property<TimeSpan>("ExecutionTimeUtc")
                    .HasColumnType("time");

                b.Property<string>("Frequency")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<bool>("IntegrityCheckEnabled")
                    .HasColumnType("bit");

                b.Property<DateTime?>("LastRunAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime?>("NextRunAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ProtectedEncryptionSecret")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Scope")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<bool>("SendEmail")
                    .HasColumnType("bit");

                b.Property<string>("StoragePath")
                    .IsRequired()
                    .HasMaxLength(1024)
                    .HasColumnType("nvarchar(1024)");

                b.HasKey("Id");

                b.ToTable("BackupSchedules");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.AppUser", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("AccessFailedCount")
                    .HasColumnType("int");

                b.Property<string>("ConcurrencyStamp")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)")
                    .HasDefaultValue("system");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Department")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("AuthenticatorKey")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)")
                    .HasDefaultValue(string.Empty);

                b.Property<string>("Email")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<bool>("EmailConfirmed")
                    .HasColumnType("bit");

                b.Property<bool>("IsDomainAccount")
                    .HasColumnType("bit");

                b.Property<bool>("LockoutEnabled")
                    .HasColumnType("bit");

                b.Property<DateTimeOffset?>("LockoutEnd")
                    .HasColumnType("datetimeoffset");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("NormalizedEmail")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("NormalizedUserName")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("PasswordHash")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("PhoneNumber")
                    .HasColumnType("nvarchar(max)");

                b.Property<bool>("PhoneNumberConfirmed")
                    .HasColumnType("bit");

                b.Property<string>("SecurityStamp")
                    .HasColumnType("nvarchar(max)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<bool>("TwoFactorEnabled")
                    .HasColumnType("bit");

                b.Property<bool>("TwoFactorRequired")
                    .HasColumnType("bit");

                b.Property<string>("UserName")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.HasKey("Id");

                b.HasIndex("NormalizedEmail");

                b.HasIndex("NormalizedUserName").IsUnique().HasFilter("[NormalizedUserName] IS NOT NULL");

                b.ToTable("AspNetUsers", (string)null);
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.AuditLog", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Action")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("ChangeSummary")
                    .HasMaxLength(400)
                    .HasColumnType("nvarchar(400)");

                b.Property<string>("ChangedFieldsJson")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid>("EntityId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("EntityType")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("IpAddress")
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<DateTime>("PerformedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("PerformedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Roles")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("UserAgent")
                    .HasMaxLength(400)
                    .HasColumnType("nvarchar(400)");

                b.HasKey("Id");

                b.ToTable("AuditLogs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ConnectorProfile", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Alias")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Configuration")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("HealthStatus")
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("Type")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.HasKey("Id");

                b.ToTable("ConnectorProfiles");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ConnectorSecret", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("ConnectorProfileId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Key")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("SecretReference")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.HasKey("Id");

                b.HasIndex("ConnectorProfileId");

                b.ToTable("ConnectorSecrets");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.DataScope", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("AppUserId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DepartmentKey")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid?>("LocationId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("ScopeType")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.HasKey("Id");

                b.HasIndex("AppUserId");

                b.ToTable("DataScopes");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LdapRoleMapping", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("GroupName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("RoleName")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("nvarchar(128)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.HasKey("Id");

                b.HasIndex("GroupName");

                b.ToTable("LdapRoleMappings");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.PasswordResetToken", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("AppUserId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CaptchaToken")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime?>("ConsumedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DeliveryAddress")
                    .HasMaxLength(300)
                    .HasColumnType("nvarchar(300)");

                b.Property<string>("DeliveryMethod")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<DateTime>("ExpiresAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("IdentityToken")
                    .IsRequired()
                    .HasMaxLength(2048)
                    .HasColumnType("nvarchar(2048)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<bool>("RequiresSmsVerification")
                    .HasColumnType("bit");

                b.Property<string>("SmsCodeHash")
                    .HasMaxLength(256)
                    .HasColumnType("nvarchar(256)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<Guid>("Token")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("AppUserId", "Status");

                b.HasIndex("Token")
                    .IsUnique();

                b.ToTable("PasswordResetTokens");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.PasswordHistoryEntry", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("PasswordHash")
                    .IsRequired()
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<Guid>("UserId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("UserId", "CreatedAtUtc");

                b.ToTable("PasswordHistoryEntries");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.UserSession", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("AppUserId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("IssuedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<bool>("IsRevoked")
                    .HasColumnType("bit");

                b.Property<DateTime>("LastSeenAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("RevokedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("RevokedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("SessionIdentifier")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("TerminationReason")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("IpAddress")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("UserAgent")
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.HasKey("Id");

                b.HasIndex("AppUserId", "IsRevoked");

                b.HasIndex("SessionIdentifier")
                    .IsUnique();

                b.ToTable("UserSessions");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.DictionaryEntry", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Description")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("DictType")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Key")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.HasKey("Id");

                b.ToTable("DictionaryEntries");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.FeatureModule", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Description")
                    .HasColumnType("nvarchar(max)");

                b.Property<bool>("Enabled")
                    .HasColumnType("bit");

                b.Property<string>("Key")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.ToTable("FeatureModules");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LabelPrintJob", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ArtifactPath")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Format")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("JobStatus")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<string>("Target")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.HasKey("Id");

                b.ToTable("LabelPrintJobs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LabelPrintJobItem", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("AssetId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("AssetType")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<int>("Copies")
                    .HasColumnType("int")
                    .HasDefaultValue(1);

                b.Property<Guid>("LabelPrintJobId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("LabelPrintJobId");

                b.ToTable("LabelPrintJobItems");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LabelTemplate", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Code")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Description")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Format")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<string>("ModifiedBy")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("ModifiedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Payload")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.HasKey("Id");

                b.ToTable("LabelTemplates");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.NetworkDevice", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("DeviceTypeId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("InventoryNumber")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<Guid>("LocationId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Manufacturer")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Model")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Notes")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid?>("PrimaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("RackPosition")
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<Guid?>("SecondaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<DateTime?>("SupportUntil")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.ToTable("NetworkDevices");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.Server", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("EnvironmentId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("InventoryNumber")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<Guid>("LocationId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Manufacturer")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Model")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Notes")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid?>("PrimaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("PurchasedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("RackPosition")
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<Guid?>("SecondaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("ServerRoleId")
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<DateTime?>("SupportUntil")
                    .HasColumnType("datetime2");

                b.Property<Guid>("OperatingSystemId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("WsusPriorityId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.ToTable("Servers");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.Workstation", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("Cpu")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("CreatedBy")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("LocationId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("LocationNote")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("MacAddress")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Notes")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid?>("OwnerId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("OwnerDepartment")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("OwnerDisplayName")
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid?>("PrimaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("PurchasedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("Ram")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<Guid?>("SecondaryAdministratorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("Status")
                    .HasColumnType("int");

                b.Property<string>("Storage")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<DateTime?>("SupportUntil")
                    .HasColumnType("datetime2");

                b.Property<Guid>("OperatingSystemId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("WorkstationTypeId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.ToTable("Workstations");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ConnectorSecret", b =>
            {
                b.HasOne("HWInventory.Domain.Entities.ConnectorProfile", "ConnectorProfile")
                    .WithMany("Secrets")
                    .HasForeignKey("ConnectorProfileId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("ConnectorProfile");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.DataScope", b =>
            {
                b.HasOne("HWInventory.Domain.Entities.AppUser", "AppUser")
                    .WithMany("DataScopes")
                    .HasForeignKey("AppUserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("AppUser");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LabelPrintJobItem", b =>
            {
                b.HasOne("HWInventory.Domain.Entities.LabelPrintJob", null)
                    .WithMany("Items")
                    .HasForeignKey("LabelPrintJobId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ReportRun", b =>
            {
                b.HasOne("HWInventory.Domain.Entities.ReportDefinition", "ReportDefinition")
                    .WithMany("Runs")
                    .HasForeignKey("ReportDefinitionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("ReportDefinition");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.NetworkDevice", b =>
            {
                b.OwnsMany("NetworkAssignments", "HWInventory.Domain.ValueObjects.NetworkEndpoint", b1 =>
                {
                    b1.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b1.Property<string>("IpAddress")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b1.Property<string>("Label")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b1.Property<Guid?>("VlanId")
                        .HasColumnType("uniqueidentifier");

                    b1.Property<Guid>("NetworkDeviceId")
                        .HasColumnType("uniqueidentifier");

                    b1.HasKey("Id");

                    b1.WithOwner().HasForeignKey("NetworkDeviceId");

                    b1.ToTable("NetworkDeviceAssignments");
                });

                b.Navigation("NetworkAssignments");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.Server", b =>
            {
                b.OwnsMany("NetworkAssignments", "HWInventory.Domain.ValueObjects.NetworkEndpoint", b1 =>
                {
                    b1.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b1.Property<string>("IpAddress")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b1.Property<string>("Label")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b1.Property<Guid?>("VlanId")
                        .HasColumnType("uniqueidentifier");

                    b1.Property<Guid>("ServerId")
                        .HasColumnType("uniqueidentifier");

                    b1.HasKey("Id");

                    b1.WithOwner().HasForeignKey("ServerId");

                    b1.ToTable("ServerNetworkAssignments");
                });

                b.Navigation("NetworkAssignments");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.Workstation", b =>
            {
                b.OwnsMany("NetworkAssignments", "HWInventory.Domain.ValueObjects.NetworkEndpoint", b1 =>
                {
                    b1.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b1.Property<string>("IpAddress")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b1.Property<string>("Label")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b1.Property<Guid?>("VlanId")
                        .HasColumnType("uniqueidentifier");

                    b1.Property<Guid>("WorkstationId")
                        .HasColumnType("uniqueidentifier");

                    b1.HasKey("Id");

                    b1.WithOwner().HasForeignKey("WorkstationId");

                    b1.ToTable("WorkstationNetworkAssignments");
                });

                b.Navigation("NetworkAssignments");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.PasswordHistoryEntry", b =>
            {
                b.HasOne("HWInventory.Domain.Entities.AppUser", "User")
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("User");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.AppUser", b =>
            {
                b.Navigation("DataScopes");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ReportDefinition", b =>
            {
                b.Navigation("Runs");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.ConnectorProfile", b =>
            {
                b.Navigation("Secrets");
            });

            modelBuilder.Entity("HWInventory.Domain.Entities.LabelPrintJob", b =>
            {
                b.Navigation("Items");
            });
#pragma warning restore 612, 618
        }
    }
}
