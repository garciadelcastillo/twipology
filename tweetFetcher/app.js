/**
 * Module dependencies.
 */

var express = require('express');
var routes = require('./routes');
var user = require('./routes/user');
var http = require('http');
var path = require('path');

var app = express();

// all environments
app.set('port', process.env.PORT || 3000);
app.set('views', path.join(__dirname, 'views'));
app.set('view engine', 'jade');
app.use(express.favicon());
app.use(express.logger('dev'));
app.use(express.json());
app.use(express.urlencoded());
app.use(express.methodOverride());
app.use(app.router);
app.use(express.static(path.join(__dirname, 'public')));

// development only
if ('development' == app.get('env')) {
	app.use(express.errorHandler());
}

app.get('/', routes.index);
app.get('/users', user.list);

http.createServer(app).listen(app.get('port'), function(){
	console.log('Express server listening on port ' + app.get('port'));
});



///////////////////////////////////////////////////

var Twitter = require('twitter'),
    fs = require('fs');

var client = new Twitter({
    consumer_key: process.env.TWITTER_CONSUMER_KEY,
	consumer_secret: process.env.TWITTER_CONSUMER_SECRET,
	access_token_key: process.env.TWITTER_ACCESS_TOKEN_KEY,
	access_token_secret: process.env.TWITTER_ACCESS_TOKEN_SECRET
});
 

// var hashtags = [
//     '#whatever', 
//     '#system', 
//     '#control', 
//     '#radical', 
//     '#surface', 
//     '#cigarette',
//     '#pink',
//     '#raw',
//     '#passage',
//     // '#passages',
//     // '#geometry',
//     '#abstract',
//     '#kindergarten',
//     '#superficial'];

var hashtags = [
    '#miami',
    '#designmiami',
    '#basel',
    '#design',
    '#fair',
    '#art',
    '#furniture',
    '#gsd',
    '#architecture',
    '#data',
    '#artbasel',
    '#miamipavillion' 
];

var tweetCount = 100,
    sourceTweets = [],
    parsedTweets = [];

fetchTweets();


function fetchTweets() {
    for (var i = 0; i < hashtags.length; i++) {
        var tag = hashtags[i];
        queryTweets(tag);
    }
};





// https://dev.twitter.com/rest/public/search
// https://dev.twitter.com/rest/reference/get/search/tweets
function queryTweets(hashtag) {
    client.get('search/tweets', {
            q: hashtag,
            count: tweetCount
        }, function(err, tweets, res) {
            if (err) {
                console.log('ERROR FOR HASHTAG ' + hashtag);
                console.log(err);
            } else {
                console.log('successfully retrieved ' + tweets.statuses.length + ' tweets for ' + hashtag);
                storeSourceTweets(tweets, hashtag);   
            }
        }
    );
};


function storeSourceTweets(tweets, hashtag) {
    var obj = {};
    obj.hashtag = hashtag;
    obj.tweet_count = tweets.statuses.length;
    obj.raw_tweets = tweets; 
    sourceTweets.push(obj);
    checkFileSave();
};




function checkFileSave() {
    if (sourceTweets.length == hashtags.length) {
        parseTweets(sourceTweets, function() {
            var now = Date.now();
            var str = JSON.stringify(sourceTweets);
            fs.writeFile('../tweetData/' + now + '_tweets_source.json', str, function(err) {
                if (err) throw err;
                console.log('sourceTweets successfully written!');
            });
            var strParsed = JSON.stringify(parsedTweets);
            fs.writeFile('../tweetData/' + now + '_tweets_parsed.json', strParsed, function(err) {
                if (err) throw err;
                console.log('parsedTweets successfully written!');
            });

        });
    }
};




function parseTweets(sourceTweets, callback) {
    for (var i = 0; i < sourceTweets.length; i++) {
        var sT = sourceTweets[i];
        var pT = {};
        pT.hashtag = sT.hashtag;
        pT.count = sT.tweet_count;
        pT.tweets = [];
        for (var len = sT.raw_tweets.statuses.length, j = 0; j < len; j++) {
            var raw = sT.raw_tweets.statuses[j];
            var parsed = {};
            parsed.id = raw.id;
            parsed.text = raw.text;
            // parsed.text_length = parsed.text.length;
            parsed.hashtag = sT.hashtag;
            parsed.hashtag_count = raw.entities.hashtags.length;

            // parsed.words = parsed.text.split(' ');
            // parsed.word_count = parsed.words.length;
            // parsed.chars = [];
            // for (var k = 0; k < parsed.words.length; k++) {
            //     parsed.chars.push(parsed.words[k].length);
            // }
            pT.tweets.push(parsed);
        }
        parsedTweets.push(pT);
        console.log('successfully parsed ' + sT.hashtag);
    }
    callback();
};
