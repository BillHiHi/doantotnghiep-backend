FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy toàn bộ source code vào container
COPY . ./

# Restore các packages
RUN dotnet restore

# Build project ra thư mục 'out'
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./

# Thiết lập Port 8080 chuẩn của .NET 8 để Railway nhận diện
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# ⚠️ BẠN NHỚ SỬA LẠI TÊN FILE DLL DƯỚI ĐÂY CHO ĐÚNG VỚI TÊN PROJECT CỦA BẠN NHÉ
ENTRYPOINT ["dotnet", "doantotnghiep_backend.dll"]