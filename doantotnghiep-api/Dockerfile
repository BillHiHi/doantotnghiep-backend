# Giai đoạn 1: Build ứng dụng
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy file .csproj và restore các thư viện (packages)
# LƯU Ý: Thay "TenProjectCuaBan.csproj" bằng tên file thực tế của bạn
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ code còn lại và build
COPY . ./
RUN dotnet publish -c Release -o out

# Giai đoạn 2: Chạy ứng dụng (Dùng image runtime nhẹ hơn)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Mở cổng 8080 (Render thường yêu cầu cổng này cho ứng dụng Web)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Tạm thời cứ giữ nguyên dòng này, nếu lỗi chúng ta sẽ xem log để sửa
ENTRYPOINT ["dotnet", "doantotnghiep-api.dll"]