# =============================================
# Multi-stage Dockerfile for Supplier Frontend
# =============================================

FROM node:20-alpine AS base

WORKDIR /app

# Copy package files
COPY package*.json ./

# =============================================
# Development Stage
# =============================================
FROM base AS development

ENV NODE_ENV=development

# Install all dependencies
RUN npm install

# Copy application code
COPY . ./

# Expose Vite dev server port
EXPOSE 5173

# Run Vite dev server
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# =============================================
# Build Stage
# =============================================
FROM base AS build

ARG VITE_API_BASE_URL=http://dotnet-api:5001

ENV NODE_ENV=production
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# Install dependencies
RUN npm ci

# Copy application code
COPY . ./

# Build application
RUN npm run build

# =============================================
# Production Stage (Nginx)
# =============================================
FROM nginx:alpine AS production

# Copy custom nginx configuration
COPY <<EOF /etc/nginx/conf.d/default.conf
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/x-javascript application/xml+rss application/javascript application/json;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # API proxy to .NET service
    location /api/ {
        proxy_pass http://dotnet-api:5001/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Uploads proxy to .NET service
    location /uploads/ {
        proxy_pass http://dotnet-api:5001/uploads/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SPA fallback
    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # Cache static assets
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # Health check endpoint
    location /healthz {
        access_log off;
        return 200 "healthy\n";
        add_header Content-Type text/plain;
    }
}
EOF

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/healthz || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
