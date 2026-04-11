FROM node:22-alpine
WORKDIR /app

# copy package.json and package-lock.json to the working directory
COPY *.json ./
# install dependencies from lock file
RUN npm ci --omit-dev

COPY . .

EXPOSE 3020
CMD ["node", "server.js"]