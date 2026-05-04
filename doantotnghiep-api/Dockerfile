# Giai đoạn 1: Build ứng dụng (Dùng SDK để biên dịch)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Tối ưu hóa cache bằng cách copy và restore các file project trước
# Nếu dự án của bạn có nhiều project (Solution), hãy copy tất cả các file .csproj vào đúng thư mục
COPY ["doantotnghiep-api.csproj", "./"]
RUN dotnet restore "doantotnghiep-api.csproj"

# Copy toàn bộ mã nguồn
COPY . .

# Build ứng dụng với cấu hình Release và tối ưu hóa cho tốc độ thực thi
RUN dotnet publish "doantotnghiep-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Giai đoạn 2: Runtime (Dùng image siêu nhẹ để chạy trên VPS 1GB RAM)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Thiết lập biến môi trường
# Trên VPS, bạn có thể dùng cổng 80 hoặc 5000 tùy ý (ở đây tôi để 5000)
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Quan trọng cho SignalR: Tăng timeout để tránh ngắt kết nối trên môi trường mạng yếu
ENV ASPNETCORE_SIGNALR_HUB_TIMEOUT=00:01:00

EXPOSE 5000

ENTRYPOINT ["dotnet", "doantotnghiep-api.dll"]