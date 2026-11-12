# .NET Project Proposal: Livestock Health and Veterinary Service Management System

Project context: Rwanda
Proposed technology: C#, ASP.NET Core MVC, Entity Framework Core, SQL Server.

## 1. Project name

Livestock Health and Veterinary Service Management System

A centralized web application connecting livestock farmers, veterinarians, and veterinary service providers to improve animal health monitoring and veterinary service delivery.

## 2. Problem statement

Livestock farming contributes significantly to Rwanda's agricultural sector, rural livelihoods, and food security. However, livestock health management can be complicated by fragmented record-keeping and limited coordination between farmers and veterinary professionals.

Farmers may rely on handwritten records to track vaccinations, diseases, treatments, and veterinary visits. This makes medical histories difficult to maintain and retrieve. Farmers may also struggle to locate veterinarians and schedule consultations.

Veterinarians may lack access to previous animal health records, making treatment follow-ups and disease monitoring more difficult.

Furthermore, fragmented disease reporting can delay the identification of potential livestock health threats.

There is therefore a need for a centralized digital platform that manages livestock health records and facilitates access to veterinary services in Rwanda.

## 3. Project objectives

General objective:

To develop a centralized digital platform for monitoring livestock health, managing animal medical records, and facilitating veterinary service delivery in Rwanda.

Specific objectives:

1. Digitize livestock registration and animal health records.
2. Connect livestock farmers with veterinary professionals.
3. Facilitate veterinary appointment scheduling and management.
4. Track animal vaccinations, diagnoses, and treatments.
5. Enable reporting and monitoring of suspected animal diseases.*
6. Generate livestock health and veterinary service reports.

## 4. Functional and non-functional requirements

### 4.1 Functional requirements

| ID   | Functional requirement                                       |
| ---- | ------------------------------------------------------------ |
| FR01 | User registration, login, and role management.               |
| FR02 | Farmers can register farms and livestock.                    |
| FR03 | Each animal receives a unique identification number.         |
| FR04 | Farmers can view individual animal health histories.         |
| FR05 | Farmers can search for veterinarians by location.            |
| FR06 | Farmers can request and schedule veterinary services.        |
| FR07 | Veterinarians can accept or reject service requests.         |
| FR08 | Veterinarians can record diagnoses and treatments.           |
| FR09 | Veterinarians can register vaccinations and follow-up dates. |
| FR10 | Farmers can report suspected livestock diseases.             |
| FR11 | The system provides appointment and vaccination reminders.   |
| FR12 | Administrators can manage users and verify veterinarians.    |
| FR13 | Administrators can generate livestock health reports.        |

### 4.2 Non-functional requirements

| Category        | Requirement                                                         |
| --------------- | ------------------------------------------------------------------- |
| Security        | Role-based access and secure authentication.                        |
| Performance     | Efficient database queries and reasonable response times.           |
| Reliability     | Regular database backups and data integrity.                        |
| Usability       | Simple, responsive, mobile-friendly interface.                      |
| Scalability     | Support increasing numbers of farmers and animals.                  |
| Maintainability | Modular code and separation of application layers.                  |
| Compatibility   | Support modern desktop and mobile browsers.                         |
| Localization    | Support English initially, with potential Kinyarwanda localization. |
| Connectivity    | Lightweight interface suitable for limited internet connectivity.   |

Rwanda-specific considerations:

- Use Rwanda's Province, District, Sector, Cell, and Village administrative structure.
- Support cattle, goats, sheep, pigs, and poultry.
- Consider zero-grazing livestock management practices.
- Design for smallholder farmers and rural veterinary professionals.
- Account for Rwanda's Law No. 058/2021 relating to the Protection of Personal Data and Privacy.

## 5. Use-case diagram

### Main actors

- Farmer: Registers livestock, manages farm information, requests veterinary services, and views health records.
- Veterinarian: Manages appointments, diagnoses diseases, records treatments, and tracks vaccinations.
- Clinic administrator: Coordinates veterinary staff and service appointments.
- System administrator: Manages accounts, verifies veterinarians, and generates reports.

## 6. Database design

The system will use a relational database implemented in Microsoft SQL Server.

### 6.1 Main database tables

| Table          | Important attributes                                                     |
| -------------- | ------------------------------------------------------------------------ |
| Users          | UserID, FullName, Email, Phone, PasswordHash, Role                       |
| Farmers        | FarmerID, UserID                                                         |
| Veterinarians  | VeterinarianID, UserID, LicenseNumber, VerificationStatus                |
| Clinics        | ClinicID, ClinicName, Address                                            |
| Farms          | FarmID, FarmerID, FarmName, District, Sector, Cell, Village              |
| Animals        | AnimalID, FarmID, Specie, TagNumber, Breed, Sex, DateOfBirth             |
| Appointments   | AppointmentID, AnimalID, VeterinarianID, AppointmentDate, Status         |
| HealthRecords  | RecordID, AnimalID, VeterinarianID, Diagnosis, Treatment, RecordDate     |
| Vaccinations   | VaccinationID, AnimalID, VaccineName, VaccinationDate, NextDueDate       |
| DiseaseReports | ReportID, AnimalID, Diagnosis, ReportedByUserID, CaseStatus, Description |


### 6.2 Database relationships

- One farmer can own multiple farms.
- One farm can contain multiple animals.
- One animal can have multiple health records.
- One animal can receive multiple vaccinations.
- One veterinarian can manage multiple appointments.
- One animal can have multiple appointments.
- One disease can appear in multiple disease reports.

### 6.4 Proposed technology stack

| Component               | Technology                       |
| ----------------------- | -------------------------------- |
| Programming language    | C#                               |
| Backend framework       | ASP.NET Core MVC                 |
| Frontend                | HTML, CSS, JavaScript, Bootstrap |
| Database                | Microsoft SQL Server             |
| ORM                     | Entity Framework Core            |
| Authentication          | ASP.NET Core Identity            |
| Development environment | Visual Studio                    |
| Version control         | Git and GitHub                   |

The application will follow a layered architecture consisting of presentation, business logic, data access, and database layers.

## 7. Project timeline

Proposed eight-week development schedule:

| Week | Activities                                             |
| ---- | ------------------------------------------------------ |
| 1    | Requirements analysis and project planning             |
| 2    | System design, UML diagrams, and database design       |
| 3    | Database implementation and ASP.NET Core setup         |
| 4    | Authentication, user roles, and farm management        |
| 5    | Livestock registration and health records              |
| 6    | Veterinary appointments and vaccination management     |
| 7    | Disease reporting, notifications, and system testing   |
| 8    | Bug fixes, documentation, deployment, and presentation |

### Suggested group task distribution

| Group member | Responsibility                       |
| ------------ | ------------------------------------ |
| Member 1     | Authentication and user management   |
| Member 2     | Farm and livestock management        |
| Member 3     | Database design and integration      |
| Member 4     | Veterinary profiles and appointments |
| Member 5     | Health records and disease reporting |
| Member 6     | Frontend, testing, and documentation |

## Technical considerations and project scope

For the initial implementation, prioritize these five modules:

1. User authentication and role management.
2. Farm and livestock registration.
3. Animal health record management.
4. Veterinary appointment management.
5. Vaccination tracking.

Disease analytics, SMS notifications, offline functionality, GPS mapping, and integration with Rwanda's Agriculture Management Information System (AMIS) can be considered future extensions. (but will not be implemented)

Relevant Rwandan institutions: MINAGRI and RAB. Their existing agricultural and veterinary initiatives should inform the system's design. The proposed application should complement existing national systems rather than replace them.

Expected outcome: A functional ASP.NET Core web application that connects farmers and veterinarians, maintains centralized livestock health records, and improves veterinary service coordination in Rwanda.