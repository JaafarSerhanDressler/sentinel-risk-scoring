// Infrastructure for the Sentinel Participant Risk Response App.
// Provisions a Linux App Service (hosts the API + Blazor WASM PWA) and an
// Azure Database for PostgreSQL Flexible Server, wired together via an app
// setting connection string.
//
// Deploy:
//   az deployment group create -g <rg> -f infra/main.bicep \
//     -p pgAdminPassword='<strong-secret>'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short prefix for resource names.')
param namePrefix string = 'riskapp'

@description('App Service plan SKU (e.g. B1, P0v3, P1v3).')
param appServiceSku string = 'B1'

@description('PostgreSQL administrator login.')
param pgAdminLogin string = 'riskadmin'

@description('PostgreSQL administrator password.')
@secure()
param pgAdminPassword string

@description('PostgreSQL version.')
param pgVersion string = '16'

@description('.NET runtime stack for Linux App Service.')
param dotnetStack string = 'DOTNETCORE|10.0'

var suffix = uniqueString(resourceGroup().id)
var planName = '${namePrefix}-plan-${suffix}'
var webName = '${namePrefix}-web-${suffix}'
var pgName = '${namePrefix}-pg-${suffix}'
var pgDbName = 'riskapp'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: appServiceSku
  }
  properties: {
    reserved: true // Linux
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: pgName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: pgVersion
    administratorLogin: pgAdminLogin
    administratorLoginPassword: pgAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource pgDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: pgDbName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow other Azure services (e.g. the Web App) to reach the server.
resource pgAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: dotnetStack
      alwaysOn: appServiceSku != 'B1' ? true : false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__Postgres'
          value: 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${pgDbName};Username=${pgAdminLogin};Password=${pgAdminPassword};Ssl Mode=Require;Trust Server Certificate=true'
        }
      ]
    }
  }
  dependsOn: [
    pgDb
    pgAllowAzure
  ]
}

output webAppName string = web.name
output webAppUrl string = 'https://${web.properties.defaultHostName}'
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName
output postgresServerName string = postgres.name
