const express = require('express');
const { createServer } = require('node:http');
const path = require('path');
const fs = require('fs');

const PORT = 3020;
const app = express();
app.use(express.json())
const server = createServer(app);

const DB_FILE = path.join(__dirname, 'db.json');
const ITEMS_PER_PAGE_DEFAULT = 20;

const clients = [];

// Helper function to load data
function loadData() {
    const dataBuffer = fs.readFileSync(DB_FILE);
    return JSON.parse(dataBuffer.toString());
}

// Helper function to save data
function saveData(data) {
    fs.writeFileSync(DB_FILE, JSON.stringify(data, null, 2));
}

const getIdKey = (resource, data) => {
    if (!data[resource] || data[resource].length === 0) {
        throw new Error(`Resource ${resource} not found or empty`);
    }
    const sampleItem = data[resource][0];
    const idKey = Object.keys(sampleItem).find(key => key.toLowerCase() === 'id' || key.toLowerCase() === `${resource}id`);
    return idKey;
}

app.get('/api/:resource/:identifier', (req, res) => {
    const { resource, identifier } = req.params;
    const data = loadData();
    const idKey = getIdKey(resource, data);
    
    const item = data[resource].find(item => String(item[idKey]) === identifier);
    if (item) {
        res.json(item);
    } else {
        res.status(404).json({ error: `Item ${identifier} not found in resource ${resource}` });
    }
});

app.get('/api/:resource', (req, res) => {
    const page = parseInt(req.query.page) || 1;
    const pageSize = parseInt(req.query.pageSize) || ITEMS_PER_PAGE_DEFAULT;

    const { resource } = req.params;
    const data = loadData();
    if (data[resource]) {
        const result = data[resource].slice((page - 1) * pageSize, page * pageSize);
        res.json(result);
    } else {
        res.status(404).json({ error: `Resource ${resource} not found` });
    }
});

app.post('/api/:resource', (req, res) => {
    const { resource } = req.params;
    const newItem = req.body;
    
    const data = loadData();
    if (!data[resource]) {
        data[resource] = [];
    }
    
    const idKey = getIdKey(resource, data);
    if (!newItem[idKey]) {
        newItem[idKey] = Date.now(); // Simple ID generation
    }
    
    data[resource].push(newItem);
    saveData(data);
    
    res.status(201).json(newItem);
});

app.put('/api/:resource/:identifier', (req, res) => {
    const { resource, identifier } = req.params;
    const updatedItem = req.body;
    
    const data = loadData();
    const idKey = getIdKey(resource, data);
    
    const index = data[resource].findIndex(item => String(item[idKey]) === identifier);
    if (index !== -1) {
        data[resource][index] = { ...data[resource][index], ...updatedItem };
        saveData(data);
        res.json(data[resource][index]);
    } else {
        res.status(404).json({ error: 'Item not found' });
    }
});

app.delete('/api/:resource/:identifier', (req, res) => {
    const { resource, identifier } = req.params;
    const data = loadData();
    const idKey = getIdKey(resource, data);
    
    const index = data[resource].findIndex(item => String(item[idKey]) === identifier);
    if (index !== -1) {
        const deletedItem = data[resource].splice(index, 1)[0];
        saveData(data);
        res.json(deletedItem);
    } else {
        res.status(404).json({ error: 'Item not found' });
    }
});

server.listen(PORT, () => {
    console.log(`Server is running on port ${PORT}`);
});