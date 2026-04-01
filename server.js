const { WebSocketServer } = require('ws');
const express = require('express');
const app = express();

app.get('/download', (req, res) => {
    res.download('C:\\Main\\programming\\ROBLOX-CUSTOM-CHAT\\C-websocket-messenger\\messenger\\bin\\Release\\net10.0-windows\\win-x64\\publish\\messenger.exe',"messenger.exe");
});

app.listen(3000, () => console.log('HTTP server on port 3000'));

const wss = new WebSocketServer({ port: 8080 });

wss.on('connection', (ws) => {
    console.log('Client connected');

    ws.on('message', (data) => {
        const msg = data.toString();

        wss.clients.forEach(client => {
            if (client.readyState === 1)
                client.send(msg);
        });
    });

    ws.on('close', () => console.log('Client disconnected'));
});
