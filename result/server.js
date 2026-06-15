var express = require('express'),
    async = require('async'),
    { Pool } = require('pg'),
    cookieParser = require('cookie-parser'),
    path = require('path'),
    app = express(),
    server = require('http').Server(app),
    io = require('socket.io')(server);

/* =========================
   PORT
========================= */
var port = process.env.PORT || 80;

/* =========================
   SOCKET.IO
========================= */
io.on('connection', function (socket) {

  socket.emit('message', { text: 'Welcome!' });

  socket.on('subscribe', function (data) {
    socket.join(data.channel);
  });
});

/* =========================
   DATABASE CONFIG (FIXED)
========================= */

// DEBUG (WAJIB untuk cek ENV masuk atau tidak)
console.log("POSTGRES_CONNECTION_STRING =", process.env.POSTGRES_CONNECTION_STRING);

// Pool PostgreSQL (NO FALLBACK, BIAR TIDAK NYASAR KE DB LAMA)
var pool = new Pool({
  connectionString: process.env.POSTGRES_CONNECTION_STRING,
  ssl: {
    rejectUnauthorized: false
  }
});

/* =========================
   WAIT FOR DB CONNECTION
========================= */
async.retry(
  { times: 1000, interval: 1000 },
  function (callback) {
    pool.connect(function (err, client, done) {
      if (err) {
        console.error("Waiting for db rozik", err);
      }
      callback(err, client);
    });
  },
  function (err, client) {
    if (err) {
      return console.error("Giving up connecting to DB");
    }

    console.log("Connected to db");
    getVotes(client);
  }
);

/* =========================
   GET VOTES LOOP
========================= */
function getVotes(client) {
  client.query(
    'SELECT vote, COUNT(id) AS count FROM votes GROUP BY vote',
    [],
    function (err, result) {

      if (err) {
        console.error("Error performing query: " + err);
      } else {
        var votes = collectVotesFromResult(result);
        io.sockets.emit("scores", JSON.stringify(votes));
      }

      setTimeout(function () {
        getVotes(client);
      }, 1000);

    }
  );
}

/* =========================
   FORMAT RESULT
========================= */
function collectVotesFromResult(result) {
  var votes = { a: 0, b: 0 };

  result.rows.forEach(function (row) {
    votes[row.vote] = parseInt(row.count);
  });

  return votes;
}

/* =========================
   EXPRESS CONFIG
========================= */
app.use(cookieParser());
app.use(express.urlencoded({ extended: true }));
app.use(express.static(__dirname + '/views'));

/* =========================
   ROUTE
========================= */
app.get('/', function (req, res) {
  res.sendFile(path.resolve(__dirname + '/views/index.html'));
});

/* =========================
   START SERVER
========================= */
server.listen(port, function () {
  console.log('App running on port ' + port);
});