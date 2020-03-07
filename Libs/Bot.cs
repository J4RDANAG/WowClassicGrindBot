﻿using Libs.Actions;
using Libs.GOAP;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Libs.NpcFinder;

namespace Libs
{
    public class Bot
    {
        private GoapAction? currentAction;
        private HashSet<GoapAction> availableActions = new HashSet<GoapAction>();
        private readonly PlayerReader playerReader;
        private readonly PlayerDirection playerDirection;
        private readonly StopMoving stopMoving;
        public readonly GoapAgent Agent;
        public readonly FollowRouteAction followRouteAction;
        public readonly WalkToCorpseAction walkToCorpseAction;
        public readonly NpcNameFinder npcNameFinder;
        private List<string> blacklist = new List<string> { "THORKA", "ZARICO" };

        public readonly RouteInfo RouteInfo;

        public bool Active { get; set; }

        public Bot(PlayerReader playerReader)
        {

            this.playerReader = playerReader;
            this.Agent = new GoapAgent(playerReader, this.availableActions, this.blacklist);

            var pathText = File.ReadAllText(@"D:\GitHub\WowPixelBot\Badlands41.json");
            var spiritText = File.ReadAllText(@"D:\GitHub\WowPixelBot\Badlands39_SpiritHealer.json");

            var pathPoints = JsonConvert.DeserializeObject<List<WowPoint>>(pathText);
            pathPoints.Reverse();
            var spiritPath = JsonConvert.DeserializeObject<List<WowPoint>>(spiritText);

            this.playerDirection = new PlayerDirection(playerReader, WowProcess);
            this.stopMoving = new StopMoving(WowProcess, playerReader);
            this.npcNameFinder = new NpcNameFinder(WowProcess);
            this.followRouteAction = new FollowRouteAction(playerReader, WowProcess, playerDirection, pathPoints, stopMoving, npcNameFinder, this.blacklist);
            this.walkToCorpseAction = new WalkToCorpseAction(playerReader, WowProcess, playerDirection, spiritPath, pathPoints, stopMoving);

            this.RouteInfo = new RouteInfo(pathPoints, spiritPath, this.followRouteAction, this.walkToCorpseAction);
        }

        public async Task DoWork()
        {
            this.currentAction = followRouteAction;

            this.availableActions.Clear();
            this.availableActions.Add(followRouteAction);
            this.availableActions.Add(new KillTargetAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new PullTargetAction(WowProcess, playerReader, npcNameFinder, stopMoving));
            this.availableActions.Add(new ApproachTargetAction(WowProcess, playerReader, stopMoving, npcNameFinder));
            this.availableActions.Add(new LootAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new PostKillLootAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new HealAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new TargetDeadAction(WowProcess, playerReader, npcNameFinder));
            this.availableActions.Add(this.walkToCorpseAction);
            this.availableActions.Add(new UseHealingPotionAction(WowProcess, playerReader));
            this.availableActions.Add(new BuffAction(WowProcess, playerReader, stopMoving));

            this.availableActions.ToList().ForEach(a => 
            {
                a.ActionEvent += this.Agent.OnActionEvent;
                a.ActionEvent += npcNameFinder.OnActionEvent;

                // tell other action about my actions
                this.availableActions.ToList().ForEach(b =>
                {
                    if (b!=a) { a.ActionEvent += b.OnActionEvent; }
                });
            });



            while (Active)
            {
                await GoapPerformAction();
            }

            await stopMoving.Stop();
            Debug.WriteLine("Stopped!");

        }

        private async Task GoapPerformAction()
        {
            if (this.Agent != null)
            {
                var newAction = await this.Agent.GetAction();

                if (newAction != null)
                {
                    if (newAction != this.currentAction)
                    {
                        this.currentAction?.DoReset();
                        this.currentAction = newAction;
                        Debug.WriteLine("---------------------------------");
                        Debug.WriteLine($"New Plan= {newAction.GetType().Name}");
                    }

                    await newAction.PerformAction();
                }
                else
                {
                    Debug.WriteLine($"New Plan= NULL");
                    Thread.Sleep(500);
                }
            }

        }

        private WowProcess? wowProcess;

        public WowProcess WowProcess
        {
            get
            {
                if (this.wowProcess == null)
                {
                    this.wowProcess = new WowProcess();
                }
                return this.wowProcess;
            }
        }
    }
}
