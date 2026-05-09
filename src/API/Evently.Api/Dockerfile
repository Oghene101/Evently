FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/API/Evently.Api/Evently.Api.csproj", "src/API/Evently.Api/"]
COPY ["src/Modules/Attendance/Evently.Modules.Attendance.Application/Evently.Modules.Attendance.Application.csproj", "src/Modules/Attendance/Evently.Modules.Attendance.Application/"]
COPY ["src/Common/Evently.Common.Application/Evently.Common.Application.csproj", "src/Common/Evently.Common.Application/"]
COPY ["src/Common/Evently.Common.Domain/Evently.Common.Domain.csproj", "src/Common/Evently.Common.Domain/"]
COPY ["src/Modules/Attendance/Evently.Modules.Attendance.Domain/Evently.Modules.Attendance.Domain.csproj", "src/Modules/Attendance/Evently.Modules.Attendance.Domain/"]
COPY ["src/Modules/Attendance/Evently.Modules.Attendance.Infrastructure/Evently.Modules.Attendance.Infrastructure.csproj", "src/Modules/Attendance/Evently.Modules.Attendance.Infrastructure/"]
COPY ["src/Common/Evently.Common.Infrastructure/Evently.Common.Infrastructure.csproj", "src/Common/Evently.Common.Infrastructure/"]
COPY ["src/Modules/Attendance/Evently.Modules.Attendance.Presentation/Evently.Modules.Attendance.Presentation.csproj", "src/Modules/Attendance/Evently.Modules.Attendance.Presentation/"]
COPY ["src/Common/Evently.Common.Presentation/Evently.Common.Presentation.csproj", "src/Common/Evently.Common.Presentation/"]
COPY ["src/Modules/Events/Evently.Modules.Events.IntegrationEvents/Evently.Modules.Events.IntegrationEvents.csproj", "src/Modules/Events/Evently.Modules.Events.IntegrationEvents/"]
COPY ["src/Modules/Ticketing/Evently.Modules.Ticketing.IntegrationEvents/Evently.Modules.Ticketing.IntegrationEvents.csproj", "src/Modules/Ticketing/Evently.Modules.Ticketing.IntegrationEvents/"]
COPY ["src/Modules/Users/Evently.Modules.Users.IntegrationEvents/Evently.Modules.Users.IntegrationEvents.csproj", "src/Modules/Users/Evently.Modules.Users.IntegrationEvents/"]
COPY ["src/Modules/Attendance/Evently.Modules.Attendance.IntegrationEvents/Evently.Modules.Attendance.IntegrationEvents.csproj", "src/Modules/Attendance/Evently.Modules.Attendance.IntegrationEvents/"]
COPY ["src/Modules/Events/Evently.Modules.Events.Infrastructure/Evently.Modules.Events.Infrastructure.csproj", "src/Modules/Events/Evently.Modules.Events.Infrastructure/"]
COPY ["src/Modules/Events/Evently.Modules.Events.Application/Evently.Modules.Events.Application.csproj", "src/Modules/Events/Evently.Modules.Events.Application/"]
COPY ["src/Modules/Events/Evently.Modules.Events.Domain/Evently.Modules.Events.Domain.csproj", "src/Modules/Events/Evently.Modules.Events.Domain/"]
COPY ["src/Modules/Events/Evently.Modules.Events.Presentation/Evently.Modules.Events.Presentation.csproj", "src/Modules/Events/Evently.Modules.Events.Presentation/"]
COPY ["src/Modules/Ticketing/Evently.Modules.Ticketing.Infrastructure/Evently.Modules.Ticketing.Infrastructure.csproj", "src/Modules/Ticketing/Evently.Modules.Ticketing.Infrastructure/"]
COPY ["src/Modules/Ticketing/Evently.Modules.Ticketing.Application/Evently.Modules.Ticketing.Application.csproj", "src/Modules/Ticketing/Evently.Modules.Ticketing.Application/"]
COPY ["src/Modules/Ticketing/Evently.Modules.Ticketing.Domain/Evently.Modules.Ticketing.Domain.csproj", "src/Modules/Ticketing/Evently.Modules.Ticketing.Domain/"]
COPY ["src/Modules/Ticketing/Evently.Modules.Ticketing.Presentation/Evently.Modules.Ticketing.Presentation.csproj", "src/Modules/Ticketing/Evently.Modules.Ticketing.Presentation/"]
COPY ["src/Modules/Users/Evently.Modules.Users.Infrastructure/Evently.Modules.Users.Infrastructure.csproj", "src/Modules/Users/Evently.Modules.Users.Infrastructure/"]
COPY ["src/Modules/Users/Evently.Modules.Users.Domain/Evently.Modules.Users.Domain.csproj", "src/Modules/Users/Evently.Modules.Users.Domain/"]
COPY ["src/Modules/Users/Evently.Modules.Users.Presentation/Evently.Modules.Users.Presentation.csproj", "src/Modules/Users/Evently.Modules.Users.Presentation/"]
COPY ["src/Modules/Users/Evently.Modules.Users.Application/Evently.Modules.Users.Application.csproj", "src/Modules/Users/Evently.Modules.Users.Application/"]
RUN dotnet restore "src/API/Evently.Api/Evently.Api.csproj"
COPY . .
WORKDIR "/src/src/API/Evently.Api"
RUN dotnet build "./Evently.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Evently.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Evently.Api.dll"]