# 🐄 Livestock Health & Veterinary Service Management System

A centralized web application connecting livestock farmers, veterinarians, and administrators to improve animal health monitoring and veterinary service delivery in Rwanda.

[![.NET](https://img.shields.io/badge/.NET-ASP.NET%20Core%20MVC-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![EF Core](https://img.shields.io/badge/ORM-Entity%20Framework%20Core-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![Bootstrap](https://img.shields.io/badge/UI-Bootstrap-7952B3?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)


---

## 📋 Table of Contents

- [Overview](#overview)
- [Problem Statement](#problem-statement)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Database Schema](#database-schema)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [User Roles](#user-roles)
- [Contributing](#contributing)
- [Team](#team)

---

## Overview

Livestock farming contributes significantly to Rwanda's agricultural sector, rural livelihoods, and food security. This system provides a digital platform for monitoring livestock health, managing animal medical records, and facilitating veterinary service delivery — replacing fragmented, paper-based processes with a modern, centralized solution.

### Objectives

1. Digitize livestock registration and animal health records
2. Connect livestock farmers with veterinary professionals
3. Facilitate veterinary appointment scheduling and management
4. Track animal vaccinations, diagnoses, and treatments
5. Enable reporting and monitoring of suspected animal diseases
6. Generate livestock health and veterinary service reports

---

## Problem Statement

- **Fragmented record-keeping** — Farmers rely on handwritten records for vaccinations, diseases, treatments, and veterinary visits, making medical histories difficult to maintain and retrieve.
- **Limited access to veterinary services** — Farmers struggle to locate veterinarians and schedule consultations efficiently.
- **Incomplete treatment follow-ups** — Veterinarians lack access to previous animal health records, complicating disease monitoring.
- **Delayed disease detection** — Fragmented disease reporting can delay identification of potential livestock health threats at regional and national levels.

---

## Features

| Module | Capabilities |
|--------|-------------|
| **Authentication & Roles** | User registration, login, role-based access control (Farmer, Veterinarian, Admin) |
| **Farm Management** | Register and manage farms with Rwanda's administrative structure (Province → District → Sector → Cell → Village) |
| **Livestock Registration** | Register animals with unique identification, track species (cattle, goats, sheep, pigs, poultry), breed, sex, and date of birth |
| **Health Records** | Record diagnoses, treatments, and case statuses; view complete animal health histories |
| **Appointments** | Farmers request appointments; veterinarians accept/reject and manage schedules |
| **Vaccination Tracking** | Log vaccinations with next-due dates; automated vaccination reminders |
| **Veterinary Search** | Search for veterinarians by clinic location |
| **Reports & Analytics** | Generate livestock health and veterinary service reports |
| **Notifications** | Appointment and vaccination reminders |
| **Admin Dashboard** | Manage users, verify veterinarian credentials, manage clinic details, generate system-wide reports |

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Language** | C# |
| **Backend** | ASP.NET Core MVC |
| **Frontend** | HTML, CSS, JavaScript, Bootstrap |
| **Database** | Microsoft SQL Server |
| **ORM** | Entity Framework Core |
| **Authentication** | ASP.NET Core Identity |
| **IDE** | Visual Studio 2022+ |
| **Version Control** | Git & GitHub |

---

## Architecture

The application follows a **layered architecture**:

```
┌─────────────────────────────────────┐
│       Presentation Layer            │
│   (Views, Razor Pages, Bootstrap)   │
├─────────────────────────────────────┤
│       Business Logic Layer          │
│     (Controllers, Services)         │
├─────────────────────────────────────┤
│       Data Access Layer             │
│  (EF Core, Repositories, DbContext) │
├─────────────────────────────────────┤
│       Database Layer                │
│       (SQL Server)                  │
└─────────────────────────────────────┘
```

---

## Database Schema

### Core Tables

| Table | Key Attributes |
|-------|---------------|
| **Users** | UserID, FullName, Email, Phone, PasswordHash, Role |
| **Clinics** | ClinicID, ClinicName, Address |
| **Farms** | FarmID, FarmerID, FarmName, District, Sector, Cell, Village |
| **Animals** | AnimalID, FarmID, Species, TagNumber, Breed, Sex, DateOfBirth |
| **Appointments** | AppointmentID, AnimalID, VeterinarianID, AppointmentDate, Status, Detail |
| **HealthRecords** | RecordID, AnimalID, VeterinarianID, Diagnosis, Treatment, RecordDate, CaseStatus |
| **Vaccinations** | VaccinationID, AnimalID, VaccineName, VaccinationDate, NextDueDate |

### Relationships

```
Farmer (1) ──── (*) Farm
Farm (1) ──── (*) Animal
Animal (1) ──── (*) HealthRecord
Animal (1) ──── (*) Vaccination
Animal (1) ──── (*) Appointment
Veterinarian (1) ──── (*) Appointment
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- [SQL Server](https://www.microsoft.com/sql-server) (Express or Developer edition)
- [Visual Studio 2022+](https://visualstudio.microsoft.com/) with the **ASP.NET and web development** workload

### Installation

1. **Clone the repository**

   ```bash
   git clone https://github.com/MontellEdmondReeves/DOT_NET_GROUP_5_PROJECT.git
   cd DOT_NET_GROUP_5_PROJECT
   ```

2. **Configure the database connection**

   Update the connection string in `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=LivestockHealthDB;Trusted_Connection=True;TrustServerCertificate=True;"  

     }
   }
   ```

3. **Apply database migrations**

   ```bash
   dotnet ef database update
   ```

4. **Run the application**

   ```bash
   dotnet run
   ```

5. **Open in browser**

   Navigate to `https://localhost:5001` (or the port shown in the console).

---

## Project Structure

```
DOT_NET_GROUP_5_PROJECT/
├── Controllers/          # MVC Controllers
├── Models/               # Entity models & view models
├── Views/                # Razor views organized by controller
├── Data/                 # DbContext & migrations
├── Services/             # Business logic services
├── wwwroot/              # Static files (CSS, JS, images)
├── appsettings.json      # App configuration
├── Program.cs            # Application entry point
└── README.md
```

---

## User Roles

| Role | Permissions |
|------|------------|
| **Farmer** | Register farms & livestock, view animal health histories, search for veterinarians, request & schedule appointments |
| **Veterinarian** | Accept/reject appointments, record diagnoses & treatments, register vaccinations, set follow-up dates, generate health reports |
| **Administrator** | Manage all user accounts, verify veterinarian credentials, manage clinic details, generate system-wide reports |

---

## Rwanda-Specific Considerations

- Uses Rwanda's administrative structure: **Province → District → Sector → Cell → Village**
- Supports common livestock types: cattle, goats, sheep, pigs, and poultry
- Accounts for **zero-grazing** livestock management practices
- Designed for **smallholder farmers** and **rural veterinary professionals**
- Compliant with **Rwanda's Law No. 058/2021** relating to Personal Data Protection and Privacy
- Lightweight interface optimized for **limited internet connectivity**
- Aligned with **MINAGRI** and **RAB** agricultural and veterinary initiatives

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m "Add your feature"`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request

Please follow existing code style and include appropriate tests for new features.

---

## Team

- MUHETO RUTAYISIRE JESS
- MONTELL EDMOND REEVES
- MUTABONWA SHARON
- NDAGIJIMANA CEDRIC
- UWITONZE PACIFIC
- INDERE Meree

---

