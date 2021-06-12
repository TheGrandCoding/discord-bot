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
    [Migration("20201121195903_addForeignKeys")]
    partial class addForeignKeys
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsAttachment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("FileName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Filed")
                        .HasColumnType("datetime2");

                    b.Property<int>("FiledBy")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("AppealsAttachments");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsExhibit", b =>
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

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsHearing", b =>
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

                    b.Property<string>("Holding")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsArbiterCase")
                        .HasColumnType("bit");

                    b.Property<bool>("Sealed")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Appeals");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMember", b =>
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

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMotion", b =>
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

                    b.Property<DateTime?>("HoldingDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("MotionType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("MovantId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("HearingId");

                    b.HasIndex("MovantId");

                    b.ToTable("AppealsMotions");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMotionFile", b =>
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

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsRuling", b =>
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

                    b.Property<int>("SubmitterId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("HearingId")
                        .IsUnique();

                    b.HasIndex("SubmitterId");

                    b.ToTable("AppealsRuling");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsWitness", b =>
                {
                    b.Property<int>("HearingId")
                        .HasColumnType("int");

                    b.Property<int>("WitnessId")
                        .HasColumnType("int");

                    b.Property<int?>("AppealsHearingId")
                        .HasColumnType("int");

                    b.Property<DateTime?>("ConcludedOn")
                        .HasColumnType("datetime2");

                    b.HasKey("HearingId", "WitnessId");

                    b.HasIndex("AppealsHearingId");

                    b.HasIndex("WitnessId");

                    b.ToTable("AppealsWitnesses");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ArbiterVote", b =>
                {
                    b.Property<int>("VoterId")
                        .HasColumnType("int");

                    b.Property<int>("VoteeId")
                        .HasColumnType("int");

                    b.Property<int>("Score")
                        .HasColumnType("int");

                    b.HasKey("VoterId", "VoteeId");

                    b.ToTable("ArbiterVote");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessBan", b =>
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

                    b.HasIndex("OperatorId");

                    b.HasIndex("TargetId");

                    b.ToTable("Bans");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessDateScore", b =>
                {
                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<int>("PlayerId")
                        .HasColumnType("int");

                    b.Property<int>("Score")
                        .HasColumnType("int");

                    b.HasKey("Date", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("ChessDateScore");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessGame", b =>
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

                    b.HasIndex("LoserId");

                    b.HasIndex("WinnerId");

                    b.HasIndex("Id", "WinnerId", "LoserId");

                    b.ToTable("Games");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessInvite", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .UseIdentityColumn();

                    b.Property<string>("Code")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Invites");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessNote", b =>
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

                    b.HasIndex("OperatorId");

                    b.HasIndex("TargetId");

                    b.ToTable("Notes");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessPlayer", b =>
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

                    b.Property<int>("Losses")
                        .HasColumnType("int");

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

                    b.Property<int>("Wins")
                        .HasColumnType("int");

                    b.Property<bool>("WithdrawnModVote")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsExhibit", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.Classes.Chess.AppealsHearing", "Hearing")
                        .WithMany("Exhibits")
                        .HasForeignKey("HearingId");

                    b.Navigation("Attachment");

                    b.Navigation("Hearing");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMember", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsHearing", "AppealHearing")
                        .WithMany("Members")
                        .HasForeignKey("AppealHearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Member")
                        .WithMany("Appeals")
                        .HasForeignKey("MemberId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AppealHearing");

                    b.Navigation("Member");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMotion", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsHearing", "Hearing")
                        .WithMany("Motions")
                        .HasForeignKey("HearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Movant")
                        .WithMany("Motions")
                        .HasForeignKey("MovantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Hearing");

                    b.Navigation("Movant");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMotionFile", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.Classes.Chess.AppealsMotion", "Motion")
                        .WithMany("Attachments")
                        .HasForeignKey("MotionId");

                    b.Navigation("Attachment");

                    b.Navigation("Motion");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsRuling", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsAttachment", "Attachment")
                        .WithMany()
                        .HasForeignKey("AttachmentId");

                    b.HasOne("DiscordBot.Classes.Chess.AppealsHearing", "Hearing")
                        .WithOne("Ruling")
                        .HasForeignKey("DiscordBot.Classes.Chess.AppealsRuling", "HearingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Submitter")
                        .WithMany()
                        .HasForeignKey("SubmitterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attachment");

                    b.Navigation("Hearing");

                    b.Navigation("Submitter");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsWitness", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.AppealsHearing", null)
                        .WithMany("Witnesses")
                        .HasForeignKey("AppealsHearingId");

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Witness")
                        .WithMany()
                        .HasForeignKey("WitnessId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Witness");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ArbiterVote", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Voter")
                        .WithMany("ArbVotes")
                        .HasForeignKey("VoterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Voter");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessBan", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Operator")
                        .WithMany()
                        .HasForeignKey("OperatorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Target")
                        .WithMany("Bans")
                        .HasForeignKey("TargetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Operator");

                    b.Navigation("Target");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessDateScore", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Player")
                        .WithMany("DateScores")
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessGame", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Loser")
                        .WithMany("GamesLost")
                        .HasForeignKey("LoserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Winner")
                        .WithMany("GamesWon")
                        .HasForeignKey("WinnerId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Loser");

                    b.Navigation("Winner");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessNote", b =>
                {
                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Operator")
                        .WithMany()
                        .HasForeignKey("OperatorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Classes.Chess.ChessPlayer", "Target")
                        .WithMany("Notes")
                        .HasForeignKey("TargetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Operator");

                    b.Navigation("Target");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsHearing", b =>
                {
                    b.Navigation("Exhibits");

                    b.Navigation("Members");

                    b.Navigation("Motions");

                    b.Navigation("Ruling");

                    b.Navigation("Witnesses");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.AppealsMotion", b =>
                {
                    b.Navigation("Attachments");
                });

            modelBuilder.Entity("DiscordBot.Classes.Chess.ChessPlayer", b =>
                {
                    b.Navigation("Appeals");

                    b.Navigation("ArbVotes");

                    b.Navigation("Bans");

                    b.Navigation("DateScores");

                    b.Navigation("GamesLost");

                    b.Navigation("GamesWon");

                    b.Navigation("Motions");

                    b.Navigation("Notes");
                });
#pragma warning restore 612, 618
        }
    }
}
#endif