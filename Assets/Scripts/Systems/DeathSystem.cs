using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Entities;

[UpdateAfter(typeof(DamageSystem))]
public class DeathSystem : ComponentSystem
{

    public struct Data
    {
        public readonly int Length;
        public EntityArray Entity;
        public GameObjectArray GameObject;
        public ComponentDataArray<Death> Deaths;
        public ComponentDataArray<Turn> Turns;
    }

    [Inject] private Data data;

    protected override void OnUpdate()
    {
        int enemiesDead = 0;
        for (int i = 0; i < data.Length; i++)
        {

            var puc = PostUpdateCommands;
            var entity = data.Entity[i];

            // For each entity that has a death component
            GameObject gameObject = data.GameObject[i];

            // Get the tile of the character and set the object in it to null
            Tile tile = BoardManagerSystem.instance.getTileFromObject(gameObject);
            if (tile != null && tile.getCharacter() == data.GameObject[i])
                tile.setCharacter(null);

            // Make the character invisible
            if (!BoardManagerSystem.instance.isTraning)
            {
                foreach (Renderer r in gameObject.GetComponentsInChildren<Renderer>())
                {
                    r.enabled = false;
                }
                // Update the dead enemy counter
                if (data.GameObject[i].tag != "Player")
                    enemiesDead++;

                // If the player dies, show the GameOver dialouge
                if (GameObject.FindGameObjectWithTag("GameOver") == null &&
                    !BoardManagerSystem.instance.noAnim && data.GameObject[i].tag == "Player")
                {
                    GameManager.instance.gameUI.showGameOver(data.GameObject[i].tag == "Player");
                }
            }
        }

        // If all the enemies of the level are dead, show the NextLevel dialogue
        if (GameObject.FindGameObjectWithTag("GameOver") == null &&
           enemiesDead == BoardManagerSystem.instance.numEnemies &&
           !BoardManagerSystem.instance.isTraning)
        {
            // If level = 10, show the EndGame dialogue
            if (BoardManagerSystem.instance.level >= 10)
                GameManager.instance.gameUI.showGameOver();
            else
                GameManager.instance.gameUI.showGameOver(false);
        }

        // Get rewards and reset the training
        // Get total reward of the ML agent
        GameObject ml = null;
        BaseAgent mlAgent = null;
        if(BoardManagerSystem.instance.doubleAgent)
        {
            ml = GameObject.FindGameObjectWithTag("Player");
        }
        else
        {
            ml = GameObject.FindGameObjectWithTag("BrainedAgent");
        }
            
        if (ml != null)
        {
            mlAgent = ml.GetComponent<BaseAgent>();
        }

        if(mlAgent != null)
        {
            // Compute the last rewards
            mlAgent.giveHpReward();
            mlAgent.giveDamageReward();

            saveStats("Assets/Resources/ml_stats.csv", ml, World.Active.GetExistingManager<ActionSystem>().mlStats, mlAgent.GetCumulativeReward());
        }

        // Get the total reward of a BT agent
        GameObject bt = null;
        bt = GameObject.FindGameObjectWithTag("BTAgent");
        if(bt != null)
        {
            BlackBoard bb = bt.GetComponent<BlackBoard>();
            bb.giveDamageReward();

            saveStats("Assets/Resources/bt_stats.csv", bt, World.Active.GetExistingManager<ActionSystem>().btStats, bb.getRew());
            bb.resetRew();
        }

        // TODO: Dopo questa istruzione inizierà un nuovo episodio, quindi
        // nella tabella delle statistiche dovrai passare alla riga successiva
        BoardManagerSystem.instance.resetTraining();

    }

    void saveStats(string fileUrl, GameObject gameObject, int[] stats, float reward) {
        EntityManager entityManager = World.Active.GetExistingManager<EntityManager>();
        using (FileStream fs = new FileStream(fileUrl, FileMode.Append, FileAccess.Write)) {
            using (StreamWriter sw = new StreamWriter(fs)) {
                int hp = entityManager.GetComponentData<Stats>(gameObject.GetComponent<Character>().Entity).hp;
                sw.WriteLine((hp != 0)+","+hp+","+stats[0]+","+stats[1]+","+stats[2]+","+stats[3]+","+System.Math.Round(reward,2));
            }
        }
    }
}
