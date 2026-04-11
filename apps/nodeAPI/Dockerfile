FROM node:22-alpine
WORKDIR /app

# copy package.json and package-lock.json to the working directory
COPY package.json ./

# install dependencies from lock file
RUN npm i --omit-dev

COPY . .

RUN ls -la
# Create db.json with empty JSON object if it doesn't exist
RUN echo "{}" > db.json

EXPOSE 3020
CMD ["node", "server.js"]