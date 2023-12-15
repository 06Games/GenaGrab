﻿// <auto-generated />
using System;
using GeneaGrab.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace GeneaGrab.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20231207171811_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.14");

            modelBuilder.Entity("GeneaGrab.Models.Indexing.Person", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Age")
                        .HasColumnType("TEXT");

                    b.Property<string>("CivilStatus")
                        .HasColumnType("TEXT");

                    b.Property<string>("FirstName")
                        .HasColumnType("TEXT");

                    b.Property<string>("LastName")
                        .HasColumnType("TEXT");

                    b.Property<string>("Notes")
                        .HasColumnType("TEXT");

                    b.Property<string>("PlaceOrigin")
                        .HasColumnType("TEXT");

                    b.Property<int>("RecordId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Sex")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("RecordId");

                    b.ToTable("Persons");
                });

            modelBuilder.Entity("GeneaGrab.Models.Indexing.Record", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ArkUrl")
                        .HasColumnType("TEXT");

                    b.Property<string>("CallNumber")
                        .HasColumnType("TEXT");

                    b.Property<string>("City")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("Date")
                        .HasColumnType("TEXT");

                    b.Property<string>("District")
                        .HasColumnType("TEXT");

                    b.Property<int>("FrameNumber")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Notes")
                        .HasColumnType("TEXT");

                    b.Property<string>("PageNumber")
                        .HasColumnType("TEXT");

                    b.Property<string>("Parish")
                        .HasColumnType("TEXT");

                    b.Property<string>("Position")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RegistryId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Road")
                        .HasColumnType("TEXT");

                    b.Property<string>("SequenceNumber")
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Records");
                });

            modelBuilder.Entity("GeneaGrab.Models.Indexing.Person", b =>
                {
                    b.HasOne("GeneaGrab.Models.Indexing.Record", "Record")
                        .WithMany("Persons")
                        .HasForeignKey("RecordId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Record");
                });

            modelBuilder.Entity("GeneaGrab.Models.Indexing.Record", b =>
                {
                    b.Navigation("Persons");
                });
#pragma warning restore 612, 618
        }
    }
}
