﻿// <auto-generated />
using System;
using Earth2ExtractorCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Earth2ExtractorCore.Migrations
{
    [DbContext(typeof(E2DB))]
    partial class E2DBModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
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

                    b.Property<string>("landFieldid")
                        .HasColumnType("nvarchar(36)");

                    b.Property<string>("ownerId")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<string>("previousOwnerId")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<decimal>("price")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("time")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.HasKey("id");

                    b.HasIndex("landFieldid");

                    b.HasIndex("ownerId");

                    b.HasIndex("previousOwnerId");

                    b.ToTable("Transactions");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.SimpleData", b =>
                {
                    b.Property<string>("userid")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<DateTime>("momenta")
                        .HasColumnType("datetime2");

                    b.Property<int>("currentPropertiesOwned")
                        .HasColumnType("int");

                    b.Property<decimal>("profitsOnSell")
                        .HasColumnType("decimal(18,2)");

                    b.Property<double>("returnsOnSell")
                        .HasColumnType("float");

                    b.Property<int>("tilesBoughtAmount")
                        .HasColumnType("int");

                    b.Property<int>("tilesCurrentlyOwned")
                        .HasColumnType("int");

                    b.Property<int>("tilesSoldAmount")
                        .HasColumnType("int");

                    b.Property<int>("totalPropertiesOwned")
                        .HasColumnType("int");

                    b.Property<int>("totalPropertiesResold")
                        .HasColumnType("int");

                    b.Property<int>("totalUniquePropertiesOwned")
                        .HasColumnType("int");

                    b.HasKey("userid", "momenta");

                    b.ToTable("Simpletons");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.User", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(36)
                        .HasColumnType("nvarchar(36)");

                    b.Property<string>("countryCode")
                        .HasMaxLength(8)
                        .HasColumnType("nvarchar(8)");

                    b.Property<string>("customPhoto")
                        .HasColumnType("nvarchar(max)");

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
                    b.HasOne("E2ExCoreLibrary.Model.LandField", "landField")
                        .WithMany("transactionSet")
                        .HasForeignKey("landFieldid");

                    b.Navigation("landField");
                });

            modelBuilder.Entity("E2ExCoreLibrary.Model.LandField", b =>
                {
                    b.Navigation("transactionSet");
                });
#pragma warning restore 612, 618
        }
    }
}
