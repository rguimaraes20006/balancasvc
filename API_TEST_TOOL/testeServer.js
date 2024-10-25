const express = require('express');
const bodyParser = require('body-parser');

const app = express();
const port = 3000;

app.use(bodyParser.json());

const authenticate = (req, res, next) => {
    const authHeader = req.headers.authorization;

    if (!authHeader) {
        return res.status(401).send('Authorization header não enviado');
    }

    const base64Credentials = authHeader.split(' ')[1];
    const credentials = Buffer.from(base64Credentials, 'base64').toString('utf8');
    const [username, password] = credentials.split(':');

    console.log('Autenticação com os dados: username:', username, 'password:', password);
    next();

};


app.post('/api/medida', authenticate, (req, res) => {
    console.log('Received payload:', req.body);
    //envia o payload recebido de volta
    res.send('Payload recebido com sucesso: ' + JSON.stringify(req.body));

});

app.listen(port, () => {
    console.log(`Server is running on http://localhost:${port}`);
});
