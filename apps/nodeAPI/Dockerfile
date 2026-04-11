FROM node:24-alpine
WORKDIR /app

# copy package.json to the working directory
COPY apps/nodeAPI/package.json ./
RUN ls -la

# install dependencies
RUN npm i --omit-dev

COPY apps/nodeAPI/ .

RUN ls -la
# Create db.json with empty JSON object if it doesn't exist
RUN echo "{}" > db.json

EXPOSE 3020
CMD ["node", "server.js"]