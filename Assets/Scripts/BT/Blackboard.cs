using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Panda;

[UpdateAfter(typeof(ActionSystem))]
public class BlackBoard : MonoBehaviour
{
    EntityManager entityManager;
    public Character agent;
    Vector3 destination;
    Func<float,float,bool> opMinor = (a,b) => a < b;
    Func<float,float,bool> opMajor = (a,b) => a > b;
    List<Vector3> movements = new List<Vector3> {new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1), new Vector3(-1, 0, 1) };

    List<Move> moves  = new List<Move>();
    bool hasSavedStats = false;

    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.Active.GetExistingManager<EntityManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if(entityManager.GetComponentData<Stats>(agent.Entity).hp == 0) {
            SaveStats(false);
        }
    }

    // save the game stats
    void SaveStats(bool victory) {
        if(!hasSavedStats) {
            using (FileStream fs = new FileStream("Assets/Resources/bt_stats.csv", FileMode.Append, FileAccess.Write)) {
                using (StreamWriter sw = new StreamWriter(fs)) {
                    int attacks = moves.FindAll(m => m.action.Equals("attack")).Count;
                    sw.WriteLine(victory+","+entityManager.GetComponentData<Stats>(agent.Entity).hp+","+moves.Count+","+attacks);
                }
            }
            hasSavedStats = true;
        }
    }

    // Check which items are near the agent
    [Task]
    bool CheckNearItems(string type)
    {
        type = (type != "warrior" && type != "archer")
        ? (new System.Random()).Next(0, 2) == 0 ? "warrior" : "archer"
        : type;
        Character agent2 = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        String currentSword = agent.GetComponent<Inventory>().meeleWeapon.itemName;
        String currentBow = agent.GetComponent<Inventory>().rangeWeapon.itemName;
        float bSwordDistance = DistanceFromNearest(typeof(MeleeWeapon), "Bastard Sword");
        float lSwordDistance = DistanceFromNearest(typeof(MeleeWeapon), "Long Sword");
        float dBowDistance = DistanceFromNearest(typeof(RangeWeapon), "Darkmoon Bow");
        float lBowDistance = DistanceFromNearest(typeof(RangeWeapon), "Long Bow");
        float healthDistance = DistanceFromNearest(typeof(HealthPotion));
        float buffDistance = DistanceFromNearest(typeof(BuffPotion));
        float enemyDistance = Vector3.Distance(agent.transform.position, agent2.transform.position);
        if (enemyDistance >= 3) {
            if (type == "warrior") {
                if(bSwordDistance < enemyDistance && currentSword != "Bastard Sword") {
                    destination = GetDestinationToNearest(typeof(MeleeWeapon), "Bastard Sword");
                    return true;
                }
                if(lSwordDistance < enemyDistance && currentSword != "Bastard Sword" && currentSword != "Long Sword") {
                    destination = GetDestinationToNearest(typeof(MeleeWeapon), "Long Sword");
                    return true;
                }
            }
            if (type == "archer") {
                if(dBowDistance < enemyDistance && currentBow != "Darkmoon Bow") {
                    destination = GetDestinationToNearest(typeof(RangeWeapon), "Darkmoon Bow");
                    return true;
                }
                if(lBowDistance < enemyDistance && currentBow != "Darkmoon Bow" && currentBow != "Long Bow") {
                    destination = GetDestinationToNearest(typeof(RangeWeapon), "Long Bow");
                    return true;
                }
            }
            if(healthDistance < enemyDistance && !HasHealthPotion() && !HasBuffActive()) {
                destination = GetDestinationToNearest(typeof(HealthPotion));
                return true;
            }
            if(buffDistance < enemyDistance && !HasBuffPotion()) {
                destination = GetDestinationToNearest(typeof(BuffPotion));
                return true;
            }
        } else {
            if(IsHealthLow() && !HasHealthPotion() && !HasBuffActive()) {
                destination = GetDestinationToNearest(typeof(HealthPotion));
                return destination != Vector3.zero && healthDistance < enemyDistance;
            }
        }
        return false;
    }

    // set Destination to Enemy
    [Task]
    bool SetDestinationToEnemy()
    {
        destination = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>().transform.position;
        return true;
    }

    // set Destination to nearest Health Potion
    [Task]
    bool SetDestinationToHealthPotion()
    {
        if (DistanceFromNearest(typeof(HealthPotion)) != float.MaxValue) {
            destination = GetDestinationToNearest(typeof(HealthPotion));
        }
        // true -> found position
        // false -> not found so the tree interrupts the execution
        return DistanceFromNearest(typeof(HealthPotion)) != float.MaxValue;
    }

    // check if the enemy is alive
    [Task]
    bool IsEnemyAlive()
    {
        Character enemy = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        bool isAlive = entityManager.GetComponentData<Stats>(enemy.Entity).hp != 0;
        if (!isAlive) {
            Task.current.Succeed();
            SaveStats(true);
        } 
        return isAlive;
    }

    // check if the enemy is near
    [Task]
    bool IsEnemyNear()
    {
        Character enemy = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        return Vector3.Distance(agent.transform.position, enemy.transform.position) < 2;
    }

    // check if the enemy is in range
    [Task]
    bool IsEnemyInRange()
    {
        Character enemy = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        return IsPositionInAgentRange(agent.transform.position, entityManager.GetComponentData<Stats>(enemy.Entity).actualRange, enemy.transform.position);
    }

    // check if agent's health is low
    [Task]
    bool IsHealthLow()
    {
        return entityManager.GetComponentData<Stats>(agent.Entity).hp < 7;
    }

    // check if agent has health potion
    [Task]
    bool HasHealthPotion()
    {
        return agent.GetComponent<Inventory>().potion != null && agent.GetComponent<Inventory>().potion is HealthPotion;
    }

    // check if agent has buff potion
    [Task]
    bool HasBuffPotion()
    {
        return agent.GetComponent<Inventory>().potion != null && agent.GetComponent<Inventory>().potion is BuffPotion;
    }

    // check if agent has buff potion activated
    [Task]
    bool HasBuffActive()
    {
        return entityManager.HasComponent(agent.Entity, typeof(Buff));
    }

    // check if a position is available
    [Task]
    bool IsPositionAvailable(Vector3 position)
    {
        Tile currentTile = BoardManagerSystem.instance.getTile((int)agent.transform.position.x, (int)agent.transform.position.z);
        Tile newTile = BoardManagerSystem.instance.getTile((int)position.x, (int)position.z);
        return newTile != null && newTile.canMove() && currentTile.isNeighbour(newTile);
    }

    // check if a position is in agent range
    [Task]
    bool IsPositionInAgentRange(Vector3 agentPosition, int range, Vector3 position)
    {
        Tile startTile = BoardManagerSystem.instance.getTile((int)agentPosition.x, (int)agentPosition.z);
        Tile positionTile = BoardManagerSystem.instance.getTile((int)position.x, (int)position.z);
        for (int i = 0; i < Enum.GetValues(typeof(DIRECTION)).Length; i++) {
            Tile prevTile = startTile;
            Vector2 startPos = new Vector2(startTile.x, startTile.y);
            Vector2 offset = GameManager.instance.directionToTile((int)Enum.GetValues(typeof(DIRECTION)).GetValue(i));
            for (int j = 0; j < range; j++)
            {
                startPos += offset;
                Tile tile = BoardManagerSystem.instance.getTile((int)startPos.x, (int)startPos.y);
                if(tile != null) {
                    if (!prevTile.isNeighbour(tile))
                        break;

                    if (positionTile != null && tile.x == positionTile.x && tile.y == positionTile.y) {
                        return true;
                    }

                    if(!tile.canMove())
                        break;

                    prevTile = tile;
                }
                
            }
        }
        return false;
    }

    // agent uses potion
    [Task]
    void UsePotion()
    {
        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = 8 }
        );
        
        Task.current.Succeed();
    }

    // move agent to destination
    [Task]
    void MoveToDestination()
    {
        int action = this.BestMoveAvailableToDestination(agent.transform.position);

        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = action }
        );
        
        moves.Add(new Move(action, "move"));
        Task.current.Succeed();
    }

    // move agent to destination
    [Task]
    void MoveToRangeDestination()
    {
        int action = this.BestMoveAvailableInRange(agent.transform.position);

        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = action }
        );
        
        moves.Add(new Move(action, "move"));
        Task.current.Succeed();
    }

    // agent melee attacks enemy
    [Task]
    void AttackEnemy()
    {
        int action = this.BestMoveAvailableToDestination(agent.transform.position, "minor", true);

        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = action }
        );
        
        moves.Add(new Move(action, "attack"));
        Task.current.Succeed();
    }

    // agent range attacks enemy
    [Task]
    void RangeAttackEnemy()
    {
        int action = this.BestMoveAvailableInRange(agent.transform.position, "minor", true);

        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = action }
        );
        
        moves.Add(new Move(action, "attack"));
        Task.current.Succeed();
    }

    // agent avoids destination
    [Task]
    void AvoidDestination()
    {
        int action = this.BestMoveAvailableToDestination(agent.transform.position, "major");

        entityManager.AddComponentData(
            agent.Entity,
            new UserInput { action = action }
        );
        
        moves.Add(new Move(action, "move"));
        Task.current.Succeed();
    }

    // find the best move based on parameters
    int BestMoveAvailableToDestination(Vector3 position, String operation = "minor", bool attack = false) {
        int newPos = (int)DIRECTION.North;
        List<int> alternativePos = new List<int>();
        float distance = operation == "major" ? float.MinValue : float.MaxValue;
        Func<float,float,bool> op = operation == "major" ? opMajor : opMinor;
        Character enemy = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        int range = entityManager.GetComponentData<Stats>(enemy.Entity).actualRange;

        for(int i = 0; i < movements.Count; i++) {
            if (
                op(Vector3.Distance(position + movements[i], destination),distance) && 
                !IsPositionInAgentRange(enemy.transform.position, range, position + movements[i]) &&
                (attack || IsPositionAvailable(position + movements[i]))
            ) {
                distance = Vector3.Distance(position + movements[i], destination);
                newPos = i;
            }
            if (IsPositionAvailable(position + movements[i])) {
                alternativePos.Add(i);
            }
        }

        if (distance == float.MaxValue || distance == float.MinValue) {
            newPos = alternativePos[(new System.Random()).Next(0, alternativePos.Count)];
        }

        return newPos;
    }

    // find the best move based on parameters in enemy range
    int BestMoveAvailableInRange(Vector3 position, String operation = "major", bool attack = false) {
        int newPos = (int)DIRECTION.North;
        List<int> alternativePos = new List<int>();
        float distance = operation == "major" ? float.MinValue : float.MaxValue;
        Func<float,float,bool> op = operation == "major" ? opMajor : opMinor;
        Character enemy = GameObject.FindGameObjectWithTag("Player").GetComponent<Character>();
        int range = entityManager.GetComponentData<Stats>(agent.Entity).actualRange;

        for(int i = 0; i < movements.Count; i++) {
            if (
                op(Vector3.Distance(position + movements[i], destination),distance) && 
                IsPositionInAgentRange(enemy.transform.position, range, position + movements[i]) &&
                (attack || IsPositionAvailable(position + movements[i]))
            ) {
                distance = Vector3.Distance(position + movements[i], destination);
                newPos = i + ((attack) ? 9 : 0);
            }
            if (IsPositionAvailable(position + movements[i]) && !IsPositionInAgentRange(enemy.transform.position, range, position + movements[i])) {
                alternativePos.Add(i + ((attack) ? 9 : 0));
            }
        }

        if (distance == float.MaxValue || distance == float.MinValue) {
            newPos = alternativePos[(new System.Random()).Next(0, alternativePos.Count)];
        }

        return newPos;
    }

    // find the distance from the nearest object of @type with @name
    float DistanceFromNearest(Type type, String name = null)
    {
        Room room = BoardManagerSystem.instance.getRoom(BoardManagerSystem.instance.currentRoomId);
        float distance = float.MaxValue;
        for (int x = 0; x < room.getSize(); x++) {
            for (int y = 0; y < room.getSize(); y++) {
                Tile t = room.getTile(x, y);
                if(t != null && t.hasItem() && t.getItem().GetType() == type) {
                    float newDistance = Vector3.Distance(agent.transform.position, t.getItem().transform.position);
                    if(newDistance < distance) {
                        // if you're looking for specific name
                        if (name == null || (name != null && t.getItem().itemName == name)) {
                            distance = newDistance;
                        }
                    }
                }
            }
        }
        return distance;
    }

    // find the destination of the nearest object of @type with @name
    Vector3 GetDestinationToNearest(Type type, String name = null)
    {
        Room room = BoardManagerSystem.instance.getRoom(BoardManagerSystem.instance.currentRoomId);
        float distance = float.MaxValue;
        Vector3 dest = Vector3.zero;
        for (int x = 0; x < room.getSize(); x++) {
            for (int y = 0; y < room.getSize(); y++) {
                Tile t = room.getTile(x, y);
                if(t != null && t.hasItem() && t.getItem().GetType() == type) {
                    float newDistance = Vector3.Distance(agent.transform.position, t.getItem().transform.position);
                    if(newDistance < distance) {
                        // if you're looking for specific name
                        if (name == null || (name != null && t.getItem().itemName == name)) {
                            distance = newDistance;
                            dest = t.getItem().transform.position;
                        }
                    }
                }
            }
        }
        return dest;
    }
    private class Move {
        public int input;
        public string action;
        public Move(int input, string action) {
            this.input = input;
            this.action = action;
        }
        public override string ToString() {
            return input + ", " + action;
        }
    }

}
