# DivisiBillWsClient

A .NET MAUI cross-platform client application for interacting with the DivisiBill Web Service. This mobile/desktop application provides a comprehensive user interface for managing file storage, billing, items, and table data through a web-based API.

## Overview

DivisiBillWsClient is built with Microsoft .NET MAUI (Multi-platform App UI) and targets both Android and Windows platforms. It enables users to connect to a DivisiBill Web Service instance and perform operations including file management, billing operations, inventory tracking, and database table operations. It also permits a connection to Azure Data storage to directly access and manage stored items.

## Project Structure

### Core Application Files

- **App.xaml / App.xaml.cs** - Application entry point and initialization
  - Configures the main application window (800x800)
  - Stores global settings including the user API key
  - Manages application-level state through `SettingsClass`

- **AppShell.xaml / AppShell.xaml.cs** - Shell navigation container
  - Defines the tab-based navigation structure
  - Routes between different pages
  - Displays dynamic title bar

- **MauiProgram.cs** - MAUI configuration
  - Initializes the MAUI app with platform-specific configurations
  - Registers MAUI Community Toolkit services
  - Configures custom fonts (OpenSans)

### Views

The application uses a **tabbed interface** with five main pages:

#### 1. **CallStatusPage** (`Views/CallStatusPage.xaml`)
   - **Purpose:** Basic Web Service configuration and connection testing
   - **Features:**
     - Base URL picker with predefined options or custom URL entry
     - Web service version and status checking
     - Cloud authentication setup (password management)
     - Response display for API calls
   - **ViewModel:** `MainPageViewModel`

#### 2. **FileActivitiesPage** (`Views/FileActivitiesPage.xaml`)
   - **Purpose:** File upload, download, and activity tracking
   - **Features:** 
     - View file activity history
     - Manage remote file operations
   - **ViewModel:** `MainPageViewModel`

#### 3. **StoredItemsPage** (`Views/StoredItemsPage.xaml`)
   - **Purpose:** Inventory and stored items management
   - **Features:**
     - Browse stored items on the server
     - Track item metadata
   - **ViewModel:** `MainPageViewModel`

#### 4. **BillingPage** (`Views/BillingPage.xaml`)
   - **Purpose:** Billing and in-app purchase management
   - **Features:**
     - View billing information
     - Manage subscriptions and purchases
   - **ViewModel:** `BillingPageViewModel`

#### 5. **TablePage** (`Views/TablePage.xaml`)
   - **Purpose:** Database table operations
   - **Features:**
     - Access remote database tables
     - View and manage table data
   - **ViewModel:** `TablePageViewModel`

### Additional Views

- **ChangePasswordPopup** (`Views/ChangePasswordPopup.xaml`) - Modal popup for password management
  - Used by the Call Status page for cloud authentication setup
  - **ViewModel:** `ChangePasswordViewModel`

### ViewModels (MVVM Pattern)

All ViewModels inherit from `ObservableObject` and use the MVVM Toolkit for property change notifications and command binding.

- **MainPageViewModel.cs** (918 lines)
  - Central view model handling most application logic, it should really be 3 view models, one each for the 3 main pages, but for now it is all in one.
  - Manages web service communication via shared `HttpClient`
  - Properties:
    - `BaseUrl` / `BaseUrlText` - Web service endpoint configuration
    - `BaseUrlChoices` - Pre-configured service URLs
    - `StatusResponse` - Display responses from API calls
    - `HasPassword` - Cloud authentication state
  - Commands:
    - `CallVersionCommand` - Fetch service version
    - `CallStatusCommand` - Check service status
    - `ChangePasswordCommand` - Update user password
    - `ClearStatusResponseCommand` - Clear response display
  - Methods:
    - `OnLoadedAsync()` - Page initialization
    - File operations (upload, download, delete)
    - Remote item management

- **BillingPageViewModel.cs**
  - Handles billing-related operations and in-app purchases
  - Manages purchase validation and subscription state

- **ChangePasswordViewModel.cs**
  - Manages password change dialog logic
  - Validates and commits password changes

- **TablePageViewModel.cs**
  - Manages database table operations
  - Handles table data retrieval and updates

### Services

- **CallWs.cs** - Web service communication layer
  - Handles HTTP requests to the DivisiBill Web Service
  - Provides methods for API operations

- **CryptManager.cs** - Cryptographic operations
  - Manages encryption/decryption for sensitive data
  - Password hashing and verification

- **Billing.cs** - Billing service integration
  - Manages in-app purchase operations
  - Handles subscription state

- **Utilities.cs** - Helper utilities
  - Common utility functions used across the application

- **CrossInAppBilling.shared.cs** - Cross-platform billing abstraction
  - Provides platform-agnostic in-app billing interface

### Models

- **BillingItem.cs** - Data model for billing information
  - Represents a billable item or invoice

- **PriceItem.cs** - Data model for pricing
  - Represents pricing information

- **AndroidPurchase.cs** - Android-specific purchase model
  - Handles Android in-app purchase details

### Converters

- **EnvironmentToStringConverter.cs** - XAML value converter
  - Converts environment variables or state objects to display strings
  - Used in bindings for conditional UI display

### Resources

- **AppIcon** - Application branding assets
- **Splash** - Splash screen displayed on startup
- **Images** - UI graphics and images
- **Fonts** - Custom fonts (OpenSans-Regular.ttf)
- **Styles** - XAML style definitions

### Platforms

Platform-specific code for Android and Windows implementations:
- Android-specific manifest configurations
- Windows-specific package definitions

## UI Architecture

### Navigation Structure

```
AppShell (TabBar)
├── Status Tab
│   └── CallStatusPage
├── Files Tab
│   └── FileActivitiesPage
├── Items Tab
│   └── StoredItemsPage
├── Billing Tab
│   └── BillingPage
└── Tables Tab
    └── TablePage
```

### Data Flow

The application uses the **MVVM pattern** with unidirectional data flow:

```
View (XAML)
    ↓
Data Binding
    ↓
ViewModel (ObservableObject with Commands)
    ↓
Services (HttpClient, CryptManager, etc.)
    ↓
Web Service / Local Storage
```

## Key Features

### Web Service Integration
- **Configurable Base URL** - Connect to different DivisiBill instances
- **API Key Management** - Store and manage authentication credentials
- **HTTP Client Pooling** - Shared `HttpClient` for efficient resource usage
- **Version Checking** - Verify service availability and version

### Authentication
- **Password Management** - Set or change cloud authentication passwords
- **Secure Credential Storage** - Uses encryption for sensitive data
- **API Key Support** - Environment-based key configuration for multiple deployments

### File Management
- File upload and download capabilities
- Activity tracking for file operations
- Remote file deletion

### Billing & In-App Purchases
- Android in-app purchase integration
- Subscription management
- Purchase validation and history

### Platform-Specific Features
- **Android:** Native purchase implementation, device integration
- **Windows:** Desktop application with touch/keyboard input support

## Configuration

### Base URL Configuration

The application provides multiple ways to configure the web service URL:

1. **Built-in Production URL** - From `Generated.BuildInfo.DivisiBillWsUri`
2. **Environment Variables:**
   - `DIVISIBILL_WS_URI_RELEASE` - Production deployment
   - `DIVISIBILL_ALTERNATE_WS_URI` - Alternate service instance
3. **Manual Entry** - Users can enter custom URLs via the UI

### API Key Management

API keys are automatically selected based on the configured base URL:
- Production URL uses production key from build info
- Environment variable URLs use corresponding environment keys
- Manual URLs require users to provide credentials

## Building and Deployment

### Target Frameworks
- `net10.0-android` - Android application
- `net10.0-windows10.0.19041.0` - Windows desktop application (when building on Windows)

### Build Configuration
- **Language Version:** Preview (Latest C# features)
- **Nullable Reference Types:** Enabled
- **XAML Compilation:** With source compilation binding support
- **Unsafe Blocks:** Enabled for interop scenarios
- **Material Design 3:** Enabled for modern UI styling

### Application Metadata
- **Display Name:** DivisiBillWsClient
- **Package ID:** com.autoplus.divisibill
- **Version:** 1.0

## Dependencies

### Primary Frameworks
- .NET MAUI (Microsoft.Maui.*)
- MVVM Community Toolkit (CommunityToolkit.Mvvm)
- MAUI Community Toolkit (CommunityToolkit.Maui)

### Features
- HTTP/JSON communication (.NET System.Net.Http.Json)
- Cryptography (.NET System.Security.Cryptography)
- File operations and storage access
- In-app billing integration

## Development Notes

### MVVM Implementation
- Uses `ObservableObject` base class for automatic property change notifications
- Implements `IRelayCommand` for command binding
- Supports property change notifications with `[NotifyPropertyChangedFor]` attributes

### Cross-Platform Considerations
- Platform-specific code in `Platforms/` directory
- Conditional compilation for Android/Windows differences
- Abstract services for cross-platform functionality

### Settings Persistence
- `SettingsClass` in `App.cs` maintains application settings
- `UserKey` property stores API credentials
- Settings persist across application sessions

## Security Considerations

- Credentials stored in `SettingsClass` should be treated as sensitive
- API keys should be protected in configuration/environment variables
- HTTPS is recommended for all web service communication
- Cryptographic operations use standard .NET cryptography APIs

## Extensibility

The architecture supports easy addition of new features:

1. **New Pages:** Add XAML page + code-behind + ViewModel
2. **New Services:** Add service class with dependency injection
3. **New Models:** Add data model class in Models/
4. **Platform Features:** Add platform-specific code in Platforms/

## License

Refer to the root repository LICENSE.txt file.
