-- Ghumo Odisha: full schema for a fresh MySQL 8+ database.
-- Generated from the EF Core migrations (InitialCreate .. AddCouponHolderAndCommission) with:
--   dotnet ef migrations script --project GhumoOdisha.Infrastructure --startup-project GhumoOdisha.Api
-- Regenerate after adding a migration. Seed data (admin user, demo trips) is inserted by the app on
-- its first Development start, not by this script.

CREATE DATABASE IF NOT EXISTS `GhumoOdisha` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE `GhumoOdisha`;

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AdminUsers` (
    `AdminUserId` int NOT NULL AUTO_INCREMENT,
    `Username` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Role` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `LastLoginAt` datetime(6) NULL,
    CONSTRAINT `PK_AdminUsers` PRIMARY KEY (`AdminUserId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Customers` (
    `CustomerId` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `PhoneNumber` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(200) CHARACTER SET utf8mb4 NULL,
    `PinHash` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `IsVerified` tinyint(1) NOT NULL,
    `FailedLoginAttempts` int NOT NULL,
    `LockoutUntil` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    `LastLoginAt` datetime(6) NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`CustomerId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Trips` (
    `TripId` int NOT NULL AUTO_INCREMENT,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `AmountPerPerson` decimal(10,2) NOT NULL,
    `IncludesBreakfast` tinyint(1) NOT NULL,
    `IncludesLunch` tinyint(1) NOT NULL,
    `IncludesDinner` tinyint(1) NOT NULL,
    `IncludesStay` tinyint(1) NOT NULL,
    `IncludesCoordinator` tinyint(1) NOT NULL,
    `Status` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Trips` PRIMARY KEY (`TripId`),
    CONSTRAINT `CK_Trip_AmountPerPerson` CHECK (AmountPerPerson >= 0)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ItineraryDays` (
    `ItineraryDayId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `DayNumber` int NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    CONSTRAINT `PK_ItineraryDays` PRIMARY KEY (`ItineraryDayId`),
    CONSTRAINT `FK_ItineraryDays_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `RoomPhotos` (
    `RoomPhotoId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_RoomPhotos` PRIMARY KEY (`RoomPhotoId`),
    CONSTRAINT `FK_RoomPhotos_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `TripDateSlots` (
    `TripDateSlotId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `TotalSeats` int NOT NULL,
    `AvailableSeats` int NOT NULL,
    `Status` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_TripDateSlots` PRIMARY KEY (`TripDateSlotId`),
    CONSTRAINT `CK_TripDateSlot_AvailableSeats_LteTotal` CHECK (AvailableSeats <= TotalSeats),
    CONSTRAINT `CK_TripDateSlot_AvailableSeats_NonNegative` CHECK (AvailableSeats >= 0),
    CONSTRAINT `CK_TripDateSlot_EndDate` CHECK (EndDate >= StartDate),
    CONSTRAINT `CK_TripDateSlot_TotalSeats` CHECK (TotalSeats > 0),
    CONSTRAINT `FK_TripDateSlots_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `TripHighlights` (
    `TripHighlightId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `PlaceName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `PhotoUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_TripHighlights` PRIMARY KEY (`TripHighlightId`),
    CONSTRAINT `FK_TripHighlights_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `TripPhotos` (
    `TripPhotoId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_TripPhotos` PRIMARY KEY (`TripPhotoId`),
    CONSTRAINT `FK_TripPhotos_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ItineraryPoints` (
    `ItineraryPointId` int NOT NULL AUTO_INCREMENT,
    `ItineraryDayId` int NOT NULL,
    `Time` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    CONSTRAINT `PK_ItineraryPoints` PRIMARY KEY (`ItineraryPointId`),
    CONSTRAINT `FK_ItineraryPoints_ItineraryDays_ItineraryDayId` FOREIGN KEY (`ItineraryDayId`) REFERENCES `ItineraryDays` (`ItineraryDayId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Bookings` (
    `BookingId` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `TripId` int NOT NULL,
    `TripDateSlotId` int NOT NULL,
    `NumberOfSeats` int NOT NULL,
    `AmountPerPerson` decimal(10,2) NOT NULL,
    `TotalAmount` decimal(10,2) NOT NULL,
    `AdvanceAmount` decimal(10,2) NOT NULL,
    `RemainingAmount` decimal(10,2) NOT NULL,
    `BookingStatus` int NOT NULL,
    `PaymentStatus` int NOT NULL,
    `BookingSource` int NOT NULL,
    `CustomerNotes` text CHARACTER SET utf8mb4 NULL,
    `AdminNotes` text CHARACTER SET utf8mb4 NULL,
    `RequestedAt` datetime(6) NOT NULL,
    `ConfirmedAt` datetime(6) NULL,
    `CancelledAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Bookings` PRIMARY KEY (`BookingId`),
    CONSTRAINT `CK_Booking_AdvanceAmount_LteTotal` CHECK (AdvanceAmount <= TotalAmount),
    CONSTRAINT `CK_Booking_AdvanceAmount_NonNegative` CHECK (AdvanceAmount >= 0),
    CONSTRAINT `CK_Booking_NumberOfSeats` CHECK (NumberOfSeats > 0),
    CONSTRAINT `CK_Booking_RemainingAmount` CHECK (RemainingAmount = TotalAmount - AdvanceAmount),
    CONSTRAINT `FK_Bookings_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`CustomerId`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Bookings_TripDateSlots_TripDateSlotId` FOREIGN KEY (`TripDateSlotId`) REFERENCES `TripDateSlots` (`TripDateSlotId`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Bookings_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_AdminUsers_Username` ON `AdminUsers` (`Username`);

CREATE INDEX `IX_Bookings_BookingStatus` ON `Bookings` (`BookingStatus`);

CREATE INDEX `IX_Bookings_CustomerId` ON `Bookings` (`CustomerId`);

CREATE INDEX `IX_Bookings_PaymentStatus` ON `Bookings` (`PaymentStatus`);

CREATE INDEX `IX_Bookings_RequestedAt` ON `Bookings` (`RequestedAt`);

CREATE INDEX `IX_Bookings_TripDateSlotId_BookingStatus` ON `Bookings` (`TripDateSlotId`, `BookingStatus`);

CREATE INDEX `IX_Bookings_TripId` ON `Bookings` (`TripId`);

CREATE INDEX `IX_Customers_Email` ON `Customers` (`Email`);

CREATE UNIQUE INDEX `IX_Customers_PhoneNumber` ON `Customers` (`PhoneNumber`);

CREATE INDEX `IX_ItineraryDays_TripId_DisplayOrder` ON `ItineraryDays` (`TripId`, `DisplayOrder`);

CREATE INDEX `IX_ItineraryPoints_ItineraryDayId_DisplayOrder` ON `ItineraryPoints` (`ItineraryDayId`, `DisplayOrder`);

CREATE INDEX `IX_RoomPhotos_TripId_DisplayOrder` ON `RoomPhotos` (`TripId`, `DisplayOrder`);

CREATE INDEX `IX_TripDateSlots_Status` ON `TripDateSlots` (`Status`);

CREATE INDEX `IX_TripDateSlots_TripId_StartDate` ON `TripDateSlots` (`TripId`, `StartDate`);

CREATE INDEX `IX_TripHighlights_TripId_DisplayOrder` ON `TripHighlights` (`TripId`, `DisplayOrder`);

CREATE INDEX `IX_TripPhotos_TripId_DisplayOrder` ON `TripPhotos` (`TripId`, `DisplayOrder`);

CREATE INDEX `IX_Trips_Status` ON `Trips` (`Status`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260918212449_InitialCreate', '9.0.0');

ALTER TABLE `Customers` MODIFY COLUMN `PinHash` varchar(500) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Bookings` ADD `ClientRequestId` char(36) COLLATE ascii_general_ci NULL;

CREATE TABLE `CustomerOtps` (
    `CustomerOtpId` int NOT NULL AUTO_INCREMENT,
    `PhoneNumber` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(150) CHARACTER SET utf8mb4 NULL,
    `OtpHash` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `AttemptCount` int NOT NULL,
    `IsUsed` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6) NULL,
    CONSTRAINT `PK_CustomerOtps` PRIMARY KEY (`CustomerOtpId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `CustomerRefreshTokens` (
    `CustomerRefreshTokenId` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `TokenHash` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `RevokedAt` datetime(6) NULL,
    CONSTRAINT `PK_CustomerRefreshTokens` PRIMARY KEY (`CustomerRefreshTokenId`),
    CONSTRAINT `FK_CustomerRefreshTokens_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`CustomerId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_Bookings_CustomerId_ClientRequestId` ON `Bookings` (`CustomerId`, `ClientRequestId`);

CREATE INDEX `IX_CustomerOtps_PhoneNumber_IsUsed` ON `CustomerOtps` (`PhoneNumber`, `IsUsed`);

CREATE INDEX `IX_CustomerRefreshTokens_CustomerId` ON `CustomerRefreshTokens` (`CustomerId`);

CREATE UNIQUE INDEX `IX_CustomerRefreshTokens_TokenHash` ON `CustomerRefreshTokens` (`TokenHash`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260920102624_AddCustomerOtpAndRefreshTokens', '9.0.0');

ALTER TABLE `Bookings` ADD `RazorpayOrderId` varchar(64) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Bookings` ADD `RazorpayPaymentId` varchar(64) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260920194158_AddRazorpayPaymentFields', '9.0.0');

ALTER TABLE `Bookings` ADD `PendingAdvanceAmount` decimal(10,2) NULL;

ALTER TABLE `Bookings` ADD `PendingCouponCodeId` int NULL;

ALTER TABLE `Bookings` ADD `PendingCouponDiscountAmount` decimal(10,2) NULL;

ALTER TABLE `Bookings` ADD `PendingDiscountAmount` decimal(10,2) NULL;

CREATE TABLE `CouponCodes` (
    `CouponCodeId` int NOT NULL AUTO_INCREMENT,
    `Code` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `DiscountAmount` decimal(10,2) NOT NULL,
    `ValidFrom` date NULL,
    `ValidUntil` date NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_CouponCodes` PRIMARY KEY (`CouponCodeId`),
    CONSTRAINT `CK_CouponCode_DiscountAmount_Positive` CHECK (DiscountAmount > 0)
) CHARACTER SET=utf8mb4;

CREATE TABLE `CouponRedemptions` (
    `CouponRedemptionId` int NOT NULL AUTO_INCREMENT,
    `CouponCodeId` int NOT NULL,
    `CustomerId` int NOT NULL,
    `BookingId` int NOT NULL,
    `DiscountAmount` decimal(10,2) NOT NULL,
    `RedeemedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_CouponRedemptions` PRIMARY KEY (`CouponRedemptionId`),
    CONSTRAINT `FK_CouponRedemptions_Bookings_BookingId` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`BookingId`) ON DELETE CASCADE,
    CONSTRAINT `FK_CouponRedemptions_CouponCodes_CouponCodeId` FOREIGN KEY (`CouponCodeId`) REFERENCES `CouponCodes` (`CouponCodeId`) ON DELETE CASCADE,
    CONSTRAINT `FK_CouponRedemptions_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`CustomerId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_CouponCodes_Code` ON `CouponCodes` (`Code`);

CREATE INDEX `IX_CouponRedemptions_BookingId` ON `CouponRedemptions` (`BookingId`);

CREATE UNIQUE INDEX `IX_CouponRedemptions_CouponCodeId_CustomerId` ON `CouponRedemptions` (`CouponCodeId`, `CustomerId`);

CREATE INDEX `IX_CouponRedemptions_CustomerId` ON `CouponRedemptions` (`CustomerId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260920201857_AddCouponsAndCancellationSupport', '9.0.0');

ALTER TABLE `Bookings` ADD `PickupPointId` int NULL;

CREATE TABLE `OrganizerPhotos` (
    `OrganizerPhotoId` int NOT NULL AUTO_INCREMENT,
    `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_OrganizerPhotos` PRIMARY KEY (`OrganizerPhotoId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PickupPoints` (
    `PickupPointId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `Location` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Time` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_PickupPoints` PRIMARY KEY (`PickupPointId`),
    CONSTRAINT `FK_PickupPoints_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `VehiclePhotos` (
    `VehiclePhotoId` int NOT NULL AUTO_INCREMENT,
    `TripId` int NOT NULL,
    `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `DisplayOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_VehiclePhotos` PRIMARY KEY (`VehiclePhotoId`),
    CONSTRAINT `FK_VehiclePhotos_Trips_TripId` FOREIGN KEY (`TripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Bookings_PickupPointId` ON `Bookings` (`PickupPointId`);

CREATE INDEX `IX_PickupPoints_TripId_DisplayOrder` ON `PickupPoints` (`TripId`, `DisplayOrder`);

CREATE INDEX `IX_VehiclePhotos_TripId_DisplayOrder` ON `VehiclePhotos` (`TripId`, `DisplayOrder`);

ALTER TABLE `Bookings` ADD CONSTRAINT `FK_Bookings_PickupPoints_PickupPointId` FOREIGN KEY (`PickupPointId`) REFERENCES `PickupPoints` (`PickupPointId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260920210828_AddVehiclePickupPointsAndOrganizerPhoto', '9.0.0');

CREATE TABLE `Destinations` (
    `DestinationId` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Slug` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Tagline` varchar(300) CHARACTER SET utf8mb4 NULL,
    `Region` varchar(200) CHARACTER SET utf8mb4 NULL,
    `HeroImageUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
    `AboutText` text CHARACTER SET utf8mb4 NULL,
    `BestSeason` varchar(100) CHARACTER SET utf8mb4 NULL,
    `DistanceFromBhubaneswar` varchar(100) CHARACTER SET utf8mb4 NULL,
    `IdealDuration` varchar(100) CHARACTER SET utf8mb4 NULL,
    `KnownFor` varchar(200) CHARACTER SET utf8mb4 NULL,
    `DisplayOrder` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Destinations` PRIMARY KEY (`DestinationId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TripDestinations` (
    `DestinationsDestinationId` int NOT NULL,
    `TripsTripId` int NOT NULL,
    CONSTRAINT `PK_TripDestinations` PRIMARY KEY (`DestinationsDestinationId`, `TripsTripId`),
    CONSTRAINT `FK_TripDestinations_Destinations_DestinationsDestinationId` FOREIGN KEY (`DestinationsDestinationId`) REFERENCES `Destinations` (`DestinationId`) ON DELETE CASCADE,
    CONSTRAINT `FK_TripDestinations_Trips_TripsTripId` FOREIGN KEY (`TripsTripId`) REFERENCES `Trips` (`TripId`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_Destinations_Slug` ON `Destinations` (`Slug`);

CREATE INDEX `IX_TripDestinations_TripsTripId` ON `TripDestinations` (`TripsTripId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260922051121_AddDestinations', '9.0.0');

ALTER TABLE `Trips` ADD `ItineraryPdfUrl` varchar(500) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `CouponCodes` ADD `IsFirstTimeCustomerOnly` tinyint(1) NOT NULL DEFAULT FALSE;

CREATE TABLE `SiteHeroPhotos` (
    `SiteHeroPhotoId` int NOT NULL AUTO_INCREMENT,
    `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_SiteHeroPhotos` PRIMARY KEY (`SiteHeroPhotoId`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260922195157_AddHeroPhotoItineraryPdfAndFirstTimeCoupon', '9.0.0');

CREATE TABLE `TermsAcceptances` (
    `TermsAcceptanceId` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `BookingId` int NOT NULL,
    `Version` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Text` text CHARACTER SET utf8mb4 NOT NULL,
    `AcceptedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_TermsAcceptances` PRIMARY KEY (`TermsAcceptanceId`),
    CONSTRAINT `FK_TermsAcceptances_Bookings_BookingId` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`BookingId`) ON DELETE RESTRICT,
    CONSTRAINT `FK_TermsAcceptances_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`CustomerId`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_TermsAcceptances_BookingId` ON `TermsAcceptances` (`BookingId`);

CREATE INDEX `IX_TermsAcceptances_CustomerId` ON `TermsAcceptances` (`CustomerId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260922200711_AddTermsAcceptance', '9.0.0');

ALTER TABLE `Bookings` ADD `RazorpayRefundId` varchar(64) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260923043526_AddRazorpayRefundId', '9.0.0');

ALTER TABLE `CouponRedemptions` ADD `CommissionAmount` decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE `CouponRedemptions` ADD `IsNewCustomer` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `CouponRedemptions` ADD `NumberOfSeats` int NOT NULL DEFAULT 0;

ALTER TABLE `CouponCodes` ADD `CommissionPerSeat` decimal(10,2) NOT NULL DEFAULT 200.0;

ALTER TABLE `CouponCodes` ADD `HolderName` varchar(100) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';

ALTER TABLE `CouponCodes` ADD CONSTRAINT `CK_CouponCode_CommissionPerSeat_NonNegative` CHECK (CommissionPerSeat >= 0);

UPDATE CouponCodes SET HolderName = Code WHERE HolderName = '';


                UPDATE CouponRedemptions r
                  JOIN Bookings b ON b.BookingId = r.BookingId
                  JOIN CouponCodes c ON c.CouponCodeId = r.CouponCodeId
                   SET r.NumberOfSeats = b.NumberOfSeats,
                       r.CommissionAmount = c.CommissionPerSeat * b.NumberOfSeats,
                       r.IsNewCustomer = NOT EXISTS (
                           SELECT 1 FROM (SELECT BookingId, CustomerId, ConfirmedAt FROM Bookings) prior
                            WHERE prior.CustomerId = r.CustomerId
                              AND prior.BookingId <> r.BookingId
                              AND prior.ConfirmedAt IS NOT NULL
                              AND prior.ConfirmedAt < r.RedeemedAt);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260923112728_AddCouponHolderAndCommission', '9.0.0');

COMMIT;

