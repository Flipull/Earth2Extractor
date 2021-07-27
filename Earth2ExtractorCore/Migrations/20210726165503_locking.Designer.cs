﻿// <auto-generated />
using System;
using Earth2ExtractorCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Earth2ExtractorCore.Migrations
{
    [DbContext(typeof(E2DB))]
    [Migration("20210726165503_locking")]
    partial class locking
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.8")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("E2ExCoreLibrary.Model.LandField", b =>
                {
                    b.Property<string>("id")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<byte>("tileClass")
                        .HasColumnType("tinyint");

                    b.Property<int>("tileCount")
                        .HasColumnType("int");

                    b.HasKey("id");

                    b.ToTable("LandFields");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.LandFieldTransactions", b =>
                {
                    b.Property<string>("id")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<string>("LandFieldid")
                        .HasColumnType("nvarchar(36)");

                    b.Property<string>("ownerId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("previousOwnerId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("price")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("time")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.HasKey("id");

                    b.HasIndex("LandFieldid");

                    b.ToTable("Transactions");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.User", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<bool>("locked")
                        .HasColumnType("bit");

                    b.Property<string>("name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("updated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.LandFieldTransactions", b =>
                {
                    b.HasOne("E2ExCoreLibrary.Model.LandField", null)
                        .WithMany("transactionSet")
                        .HasForeignKey("LandFieldid");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.LandField", b =>
                {
                    b.Navigation("transactionSet");
                });
#pragma warning restore 612, 618
        }
    }
}
