using System.ComponentModel.DataAnnotations;
using System.Text;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Identity;
namespace IPFMMS.LiveAdminSetup;
public static class LiveAdminSql
{
    public static string Generate(RegisterViewModel input)
    {
        input.FullName=input.FullName.Trim();input.Email=input.Email.Trim().ToLowerInvariant();
        input.PhoneNumber=string.IsNullOrWhiteSpace(input.PhoneNumber)?null:input.PhoneNumber.Trim();
        var errors=new List<ValidationResult>();
        if(!Validator.TryValidateObject(input,new ValidationContext(input),errors,true))
            throw new InvalidOperationException(string.Join(Environment.NewLine,errors.Select(e=>e.ErrorMessage)));
        var user=new User{UserId="UID00000"};
        var hash=new PasswordHasher<User>().HashPassword(user,input.Password);
        static string SqlText(string? text)=>text==null?"NULL":"CONVERT(X'"+Convert.ToHexString(Encoding.UTF8.GetBytes(text))+"' USING utf8mb4)";
        return $"""
-- One-time first LIVE administrator setup. Generated offline; never executed by this tool.
-- Contains a password hash: keep private, do not commit or paste this file into chat.
-- Run the ENTIRE file using your Azure MySQL connection after applying AddAdminManagement.
-- Creates UID00000 as Active/Admin directly for the trusted database owner; no OTP is sent.
USE `ipfmms`;
DROP PROCEDURE IF EXISTS `IPFMMS_Bootstrap_UID00000`;
DELIMITER //
CREATE PROCEDURE `IPFMMS_Bootstrap_UID00000`()
BEGIN
    DECLARE existing_count BIGINT DEFAULT 0;
    DECLARE audit_number BIGINT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    IF NOT EXISTS (SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId`='20261109113958_AddAdminManagement') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Apply and verify AddAdminManagement before creating the live Admin.';
    END IF;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    START TRANSACTION;
    SELECT COUNT(*) INTO existing_count FROM `Users` FOR UPDATE;
    IF EXISTS (SELECT 1 FROM `Users` WHERE `UserId`='UID00000') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='UID00000 already exists. No account was overwritten.';
    END IF;
    IF EXISTS (SELECT 1 FROM `Users` WHERE `Email`={SqlText(input.Email)}) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='This email is already registered. No account was overwritten.';
    END IF;
    IF EXISTS (SELECT 1 FROM `Users` WHERE `Role`='Admin' AND `AccountStatus`='Active') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='An active Admin already exists. This script is for the first live Admin only.';
    END IF;
    INSERT INTO `Users` (`UserId`,`FullName`,`Email`,`PasswordHash`,`PhoneNumber`,`Role`,`AccountStatus`,`SecurityVersion`,`CreatedDate`)
    VALUES ('UID00000',{SqlText(input.FullName)},{SqlText(input.Email)},{SqlText(hash)},{SqlText(input.PhoneNumber)},'Admin','Active',1,UTC_TIMESTAMP(6));
    INSERT INTO `IdCounters` (`Prefix`,`LastNumber`) VALUES ('AUD',LAST_INSERT_ID(1))
    ON DUPLICATE KEY UPDATE `LastNumber`=LAST_INSERT_ID(`LastNumber`+1);
    SET audit_number=LAST_INSERT_ID();
    INSERT INTO `AdminAuditEntries` (`AuditId`,`AdminUserId`,`EntityType`,`EntityId`,`Action`,`Summary`,`CreatedDate`)
    VALUES (CONCAT('AUD',LPAD(CAST(audit_number AS CHAR),GREATEST(5,CHAR_LENGTH(CAST(audit_number AS CHAR))),'0')),
            'UID00000','User','UID00000','BootstrapAdmin','First live administrator initialized by the database owner.',UTC_TIMESTAMP(6));
    COMMIT;
END //
DELIMITER ;
CALL `IPFMMS_Bootstrap_UID00000`();
DROP PROCEDURE `IPFMMS_Bootstrap_UID00000`;
SELECT `UserId`,`FullName`,`Email`,`Role`,`AccountStatus` FROM `Users` WHERE `UserId`='UID00000';
""";
    }
}
