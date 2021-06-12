﻿#if INCLUDE_CHESS
// <auto-generated />
using System;
using DiscordBot.Classes.Chess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Migrations
{
    [DbContext(typeof(ChessDbContext))]
    [Migration("20201121110322_addChess")]
    partial class addChess
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("DiscordBot.AppealsAttachment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<DateTime>("Filed")
                        .HasColumnType("datetime2");

                    b.Property<int>("FiledBy")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("AppealsAttachments");
                });

            modelBuilder.Entity("DiscordBot.AppealsExhibit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int?>("AttachmentId")
                        .HasColumnType("int");

                    b.Property<int?>("HearingId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("HearingId");

                    b.ToTable("AppealsExhibits");
                });

            modelBuilder.Entity("DiscordBot.AppealsHearing", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int?>("AppealOf")
                        .HasColumnType("int");

                    b.Property<DateTime?>("Commenced")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("Concluded")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("Filed")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsArbiterCase")
                        .HasColumnType("bit");

                    b.Property<bool>("Sealed")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Appeals");
                });

            modelBuilder.Entity("DiscordBot.AppealsMember", b =>
                {
                    b.Property<int>("MemberId")
                        .HasColumnType("int");

                    b.Property<int>("AppealHearingId")
                        .HasColumnType("int");

                    b.Property<int>("Relation")
                        .HasColumnType("int");

                    b.HasKey("MemberId", "AppealHearingId");

                    b.HasIndex("AppealHearingId");

                    b.ToTable("AppealsRelations");
                });

            modelBuilder.Entity("DiscordBot.AppealsMotion", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<DateTime>("Filed")
                        .HasColumnType("datetime2");

                    b.Property<int>("HearingId")
                        .HasColumnType("int");

                    b.Property<string>("Holding")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("MovantId")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("HearingId");

                    b.ToTable("AppealsMotions");
                });

            modelBuilder.Entity("DiscordBot.AppealsMotionFile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int?>("AttachmentId")
                        .HasColumnType("int");

                    b.Property<int?>("MotionId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("MotionId");

                    b.ToTable("AppealsMotionFiles");
                });

            modelBuilder.Entity("DiscordBot.AppealsRuling", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int?>("AttachmentId")
                        .HasColumnType("int");

                    b.Property<int>("HearingId")
                        .HasColumnType("int");

                    b.Property<string>("Holding")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("HearingId")
                        .IsUnique();

                    b.ToTable("AppealsRuling");
                });

            modelBuilder.Entity("DiscordBot.Classes.DbChess.ChessBan", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("GivenAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("OperatorId")
                        .HasColumnType("int");

                    b.Property<string>("Reason")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TargetId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Bans");
                });

            modelBuilder.Entity("DiscordBot.Classes.DbChess.ChessGame", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("ApprovalGiven")
                        .HasColumnType("int");

                    b.Property<int>("ApprovalNeeded")
                        .HasColumnType("int");

                    b.Property<bool>("Draw")
                        .HasColumnType("bit");

                    b.Property<int>("LoserChange")
                        .HasColumnType("int");

                    b.Property<int>("LoserId")
                        .HasColumnType("int");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("datetime2");

                    b.Property<int>("WinnerChange")
                        .HasColumnType("int");

                    b.Property<int>("WinnerId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("Id", "WinnerId", "LoserId");

                    b.ToTable("Games");
                });

            modelBuilder.Entity("DiscordBot.Classes.DbChess.ChessNote", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("ExpiresInDays")
                        .HasColumnType("int");

                    b.Property<DateTime>("GivenAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("OperatorId")
                        .HasColumnType("int");

                    b.Property<int>("TargetId")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Notes");
                });

            modelBuilder.Entity("DiscordBot.Classes.DbChess.ChessPlayer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<DateTime?>("DateLastPresent")
                        .HasColumnType("datetime2");

                    b.Property<long>("DiscordAccount")
                        .HasColumnType("bigint");

                    b.Property<string>("DismissalReason")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsBuiltInAccount")
                        .HasColumnType("bit");

                    b.Property<int>("Modifier")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Permission")
                        .HasColumnType("int");

                    b.Property<int>("Rating")
                        .HasColumnType("int");

                    b.Property<bool>("Removed")
                        .HasColumnType("bit");

                    b.Property<bool>("RequireGameApproval")
                        .HasColumnType("bit");

                    b.Property<bool>("RequireTiming")
                        .HasColumnType("bit");

                    b.Property<bool>("WithdrawnModVote")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("DiscordBot.AppealsExhibit", b =>
                {
                    b.HasOne("DiscordBot.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.AppealsHearing", "Hearing")
                        .WithMany("Exhibits")
                        .HasForeignKey("HearingId");

                    b.Navigation("Attachment");

                    b.Navigation("Hearing");
                });

            modelBuilder.Entity("DiscordBot.AppealsMember", b =>
                {
                    b.HasOne("DiscordBot.AppealsHearing", "AppealHearing")
                        .WithMany("Members")
                        .HasForeignKey("AppealHearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AppealHearing");
                });

            modelBuilder.Entity("DiscordBot.AppealsMotion", b =>
                {
                    b.HasOne("DiscordBot.AppealsHearing", "Hearing")
                        .WithMany("Motions")
                        .HasForeignKey("HearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Hearing");
                });

            modelBuilder.Entity("DiscordBot.AppealsMotionFile", b =>
                {
                    b.HasOne("DiscordBot.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.AppealsMotion", "Motion")
                        .WithMany("Files")
                        .HasForeignKey("MotionId");

                    b.Navigation("Attachment");

                    b.Navigation("Motion");
                });

            modelBuilder.Entity("DiscordBot.AppealsRuling", b =>
                {
                    b.HasOne("DiscordBot.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.AppealsHearing", "Hearing")
                        .WithOne("Ruling")
                        .HasForeignKey("DiscordBot.AppealsRuling", "HearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attachment");

                    b.Navigation("Hearing");
                });

            modelBuilder.Entity("DiscordBot.AppealsHearing", b =>
                {
                    b.Navigation("Exhibits");

                    b.Navigation("Members");

                    b.Navigation("Motions");

                    b.Navigation("Ruling");
                });

            modelBuilder.Entity("DiscordBot.AppealsMotion", b =>
                {
                    b.Navigation("Files");
                });
#pragma warning restore 612, 618
        }
    }
}
#endif