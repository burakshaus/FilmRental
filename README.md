# FilmRental

A comprehensive web application for managing and renting movies, featuring an AI-powered assistant for personalized movie recommendations.

## Features
- **Movie Catalog:** Browse and detail view for an extensive collection of movies.
- **Rental System:** Track rentals and available stock.
- **AI Chat Assistant:** Ask for movie recommendations using natural language, powered by Google's Gemini 2.0 Flash and embeddings.

## Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (or Docker for SQL Server)

## Getting Started

### 1. Clone the repository
```bash
git clone https://github.com/burakshaus/FilmRental.git
cd FilmRental/FilmRental
```

### 2. Configure Database
Update the `DefaultConnection` string in `appsettings.json` if necessary, then create the database:
```bash
dotnet ef database update
```

### 3. Setup the AI Assistant API Key (IMPORTANT!)
For security reasons, the **Gemini API Key is NOT included** in this repository. The application uses `.NET User Secrets` to manage sensitive API keys locally to prevent them from leaking on GitHub.

To enable the AI Chat Assistant, you must configure your own free Gemini API key:
1. Get a free API key from [Google AI Studio](https://aistudio.google.com/app/apikey).
2. Open your terminal in the `FilmRental/FilmRental` directory where the `.csproj` file is located.
3. Run the following command to securely save your API key to your local machine:
   ```bash
   dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY_HERE"
   ```

### 4. Run the Project
```bash
dotnet run
```
Navigate to `http://localhost:5295` (or the URL provided in your console) to preview the application and test out the AI chat features!
