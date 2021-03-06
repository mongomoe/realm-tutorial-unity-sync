//
//  RealmController.cs
//
//
//  Copyright © 2020-2021 MongoDB, Inc. All rights reserved.
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Realms;
using UnityEngine.UI;
using MongoDB.Bson;
using System.Threading.Tasks;
using Realms.Sync;
using System.Linq;

public class RealmController : MonoBehaviour
{
    private static Realm realm;
    
    private static int runTime; // total amount of time you've been playing during this playthrough/run (losing/winning resets runtime)
    private static int bonusPoints = 0; // start with 0 bonus points and at the end of the game we add bonus points based on how long you played

    public static Player currentPlayer; // current logged in player
    public static Stat currentStat; // current stats for this run/playthrough
    private static App realmApp = App.Create(Constants.Realm.AppId); // sync variable
    public static Realms.Sync.User syncUser; // sync variable

    // LOCAL GetRealm:
    //public static Realm GetRealm()
    //{
    //    realm = Realm.GetInstance();
    //    return realm;
    //}
    // LOCAL setLoggedInUser:
    //public static void setLoggedInUser(string loggedInUser)
    //{
    //    Debug.Log("setLoggedInUser called - " + loggedInUser);
    //    realm = GetRealm();
    //    var matchedPlayers = realm.All<Player>().Where(p => p.Name == loggedInUser);

    //    if (matchedPlayers.Count() > 0) // if the player exists
    //    {
    //        Debug.Log("Hello");
    //        currentPlayer = matchedPlayers.First();
    //        var s1 = new Stat();
    //        s1.StatOwner = currentPlayer;

    //        realm.Write(() =>
    //        {
    //            currentStat = realm.Add(s1);
    //            currentPlayer.Stats.Add(currentStat);
    //        });
    //    }
    //    else
    //    {
    //        Debug.Log("Ho");
    //        var p1 = new Player();
    //        p1.Id = ObjectId.GenerateNewId().ToString();
    //        p1.Name = loggedInUser;

    //        var s1 = new Stat();
    //        s1.StatOwner = p1;

    //        realm.Write(() =>
    //        {
    //            currentPlayer = realm.Add(p1);
    //            currentStat = realm.Add(s1);
    //            currentPlayer.Stats.Add(currentStat);
    //        });
    //    }

    //    startGame();
    //}

    // SYNC setLoggedInUser:
    public static async Task<Player> setLoggedInUser(string userInput, string passInput)
    {        
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        if (syncUser != null)
        {
            realm = await GetRealm(syncUser);
            Debug.Log($"Realm is located at: {realm.Config.DatabasePath}");


            currentPlayer = realm.Find<Player>(syncUser.Id);

            if (currentPlayer != null)
            {
                var s1 = new Stat();
                s1.StatOwner = currentPlayer;

                realm.Write(() =>
                {
                    currentStat = realm.Add(s1);
                    currentPlayer.Stats.Add(currentStat);
                });

                startGame();
            }
            else
            {
                Debug.Log("This player exists a MongoDB Realm User but not as a Realm Object, please delete the Sync User and create one ussing the register button");
            }
        }

        return currentPlayer;
    }

    // sync method:
    // when app.EmailPasswordAuth.RegisterUserAsync is called, then a new player should be created
    public static async Task<Player> OnPressRegister(string userInput, string passInput)
    {
        await realmApp.EmailPasswordAuth.RegisterUserAsync(userInput, passInput);
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        realm = await GetRealm(syncUser);
        Debug.Log($"Realm is located at: {realm.Config.DatabasePath}");

        var p1 = new Player();
        p1.Id = syncUser.Id;
        p1.Name = userInput;

        var s1 = new Stat();
        s1.StatOwner = p1;


        realm.Write(() =>
        {
            currentPlayer = realm.Add(p1);
            currentStat = realm.Add(s1);
            currentPlayer.Stats.Add(currentStat);
        });

        startGame();

        Debug.Log("Game has started " + currentPlayer.Name);

        return currentPlayer;
    }


    // sync method:
    public static async Task<Realm> GetRealm(Realms.Sync.User loggedInUser) // Realms.Sync.User
    {
        var syncConfiguration = new SyncConfiguration("UnityTutorialPartition", loggedInUser);
        return await Realm.GetInstanceAsync(syncConfiguration);
    }






    // startGame() begins the timer, increasing runTime every 10 seconds.
    // The less time a player takes to complete the playthrough/run, the more bonusPoints the player is rewarded
    public static void startGame()
    {
        // record each 10 seconds (runTime will be used to calculate bonus points once the player wins the game)
        var myTimer = new System.Timers.Timer(10000);
        myTimer.Enabled = true;
        myTimer.Elapsed += (sender, e) => runTime += 10;
    }

    public static void collectToken() // performs an update on the Character Model's token count
    {
        realm.Write(() =>
        {
            currentStat.TokensCollected += 1;
        });
    }
    public static void defeatEnemy() // performs an update on the Character Model's enemiesDefeated Count
    {
        realm.Write(() =>
        {
            currentStat.EnemiesDefeated += 1;
        });
    }

    // deleteCurrentScore deletes the stats of the player's current playthrough/run and unregisters the listener on the old stat
    // deleteCurrentScore is typically called on the "PlayerDeath" event
    public static void deleteCurrentScore()
    {
        ScoreCardManager.unRegisterListener();

        realm.Write(() =>
        {
            realm.Remove(currentStat);
            currentPlayer.Stats.Remove(currentStat);
        });
    }
    public static void restartGame()
    {
        if (currentPlayer != null)
        {
            var s1 = new Stat();

            realm.Write(() =>
            {
                currentStat = realm.Add(s1);
                currentPlayer.Stats.Add(currentStat);
            });

            ScoreCardManager.setCurrentStat(currentStat); // call `setCurrentStat()` to set the current stat in the UI using ScoreCardManager
            ScoreCardManager.watchForChangesToCurrentStats(); // call `watchForChangesToCurrentStats()` to register a listener on the new score in the ScoreCardManager

            startGame(); // start the game by resetting the timer and officially starting a new run/playthrough
        }
    }

    public static int[] playerWon()
    {
        if (runTime <= 30) // if the game is beat in in less than or equal to 30 seconds, +80 bonus points
        {
            bonusPoints = 80;
        }
        else if (runTime <= 60) // if the game is beat in in less than or equal to 1 min, +70 bonus points
        {
            bonusPoints = 70;
        }
        else if (runTime <= 90) // if the game is beat in less than or equal to 1 min 30 seconds, +60 bonus points
        {
            bonusPoints = 60;
        }
        else if (runTime <= 120) // if the game is beat in less than or equal to 2 mins, +50 bonus points
        {
            bonusPoints = 50;
        }

        // calculate final points + write to realm with points
        var finalScore = calculatePoints();
        realm.Write(() =>
        {
            currentStat.Score = finalScore;
        });


        var scoreAndBonusPoints = new int[2];
        scoreAndBonusPoints[0] = finalScore;
        scoreAndBonusPoints[1] = bonusPoints;

        return scoreAndBonusPoints; // return the final score and bonus points to display to the UI
    }

    public static int calculatePoints()
    {
        return (currentStat.EnemiesDefeated + 1) * (currentStat.TokensCollected + 1) + bonusPoints;
    }
}