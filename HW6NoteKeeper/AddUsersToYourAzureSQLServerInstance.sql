-- Replace the <user principal name> with the User Principal Name for each user listed below
-- The User principal name can be found in the Azure Active Directory Users listing in your Azure AD Tenant
-- on the Overview tab for each user

-- Jonathan Franck
CREATE USER [jon001sox_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [jon001sox_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_datawriter ADD MEMBER [jon001sox_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_ddladmin ADD MEMBER [jon001sox_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];

-- Max Eringros
CREATE USER [meringros_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [meringros_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_datawriter ADD MEMBER [meringros_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_ddladmin ADD MEMBER [meringros_gmail.com#EXT#@shawnjosephazure.onmicrosoft.com];

-- Jessica Pratt
CREATE USER [jphvdta_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [jphvdta_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_datawriter ADD MEMBER [jphvdta_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_ddladmin ADD MEMBER [jphvdta_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];

-- Joseph Ficara
CREATE USER [jficaraharvardinstructor_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [jficaraharvardinstructor_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_datawriter ADD MEMBER [jficaraharvardinstructor_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];
ALTER ROLE db_ddladmin ADD MEMBER [jficaraharvardinstructor_outlook.com#EXT#@shawnjosephazure.onmicrosoft.com];

GO