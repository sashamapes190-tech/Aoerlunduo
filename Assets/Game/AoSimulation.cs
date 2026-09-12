using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Data;
using UnityEngine;

namespace Aoerlunduo
{
    [Serializable] public class Nation
    {
        public string id, name, capital;
        public float x, y;
        public int owner, troops = 8, farms = 1, forts, population = 100;
        public long treasury = 200L << 32;
        public List<int> neighbors = new List<int>();
        public FixedPoint64 Gold { get => FixedPoint64.FromRaw(treasury); set => treasury = value.RawValue; }
    }
    [Serializable] public class Campaign
    {
        public int version = 1, year = 2136, month = 1, player = -1, seed = 2136, pendingEvent = -1;
        public List<Nation> nations = new List<Nation>();
        public List<string> wars = new List<string>();
    }
    public struct WorldChanged : IGameEvent { public float TimeStamp {get;set;} }
    public sealed class AoSimulation : IDisposable
    {
        public Campaign State;
        public readonly EventBus Events = new EventBus();
        public string Notice = "选择一个国家，开启 AO 2136 年的历史。";
        static FixedPoint64 Money(int n) => FixedPoint64.FromInt(n);
        public AoSimulation() { State = Create(); }
        public void Dispose() => Events.Dispose();
        public void Changed(string message) { Notice=message; Events.Emit(new WorldChanged()); Events.ProcessEvents(); }
        static string WarKey(int a,int b) => Math.Min(a,b)+":"+Math.Max(a,b);
        public bool AtWar(int a,int b) => State.wars.Contains(WarKey(a,b));
        public int Territory(int owner) => State.nations.Count(n=>n.owner==owner);
        public int Income(int owner) => State.nations.Where(n=>n.owner==owner).Sum(n=>8+n.farms*6-n.troops);
        public bool Select(int id) { if(State.player>=0||id<0||id>=State.nations.Count)return false; State.player=id; Changed("你将领导 "+State.nations[id].name+"。"); return true; }
        public bool Build(int id,bool fort)
        {
            var n=State.nations[id]; int price=fort?90:70;
            if(State.pendingEvent>=0||State.player<0||n.owner!=State.player||State.nations[State.player].Gold<Money(price))return false;
            State.nations[State.player].Gold-=Money(price); if(fort)n.forts++;else n.farms++;
            Changed(n.name+(fort?"修筑了要塞。":"扩建了农田。"));return true;
        }
        public bool Recruit(int id)
        {
            var n=State.nations[id];if(State.pendingEvent>=0||State.player<0||n.owner!=State.player||State.nations[State.player].Gold<Money(50)||n.population<10)return false;
            State.nations[State.player].Gold-=Money(50); n.troops+=5;n.population-=10;Changed("已招募 5 个军团，每月增加 5 金军费。");return true;
        }
        public bool Diplomacy(int target)
        {
            int p=State.player;if(p<0||State.pendingEvent>=0)return false;int owner=State.nations[target].owner;
            if(owner==p||!State.nations.Any(n=>n.owner==p&&n.neighbors.Any(i=>State.nations[i].owner==owner)))return false;
            string key=WarKey(p,owner);if(!State.wars.Remove(key)){State.wars.Add(key);Changed("已向 "+State.nations[owner].name+" 宣战。");}else Changed("双方签署停战协议。");return true;
        }
        public bool March(int source,int target)
        {
            if(State.pendingEvent>=0||source<0||source==target)return false;
            var a=State.nations[source];var b=State.nations[target];
            if(a.owner!=State.player||a.troops<2||!a.neighbors.Contains(target))return false;
            if(b.owner!=a.owner&&!AtWar(a.owner,b.owner))return false;
            Resolve(source,target);return true;
        }
        void Resolve(int source,int target)
        {
            var a=State.nations[source];var b=State.nations[target];int force=a.troops-1;a.troops=1;
            if(a.owner==b.owner){b.troops+=force;Changed("军团已抵达 "+b.name+"。");return;}
            int defense=b.troops+b.forts*4;
            if(force>defense){b.troops=Math.Max(1,force-defense);b.owner=a.owner;Changed(b.name+" 已被占领。");}
            else {b.troops=Math.Max(1,b.troops-force/2);Changed("进攻 "+b.name+" 失败，守军守住了阵地。");}
        }
        int Random(int max){ State.seed=unchecked(State.seed*1664525+1013904223);return (int)((uint)State.seed%(uint)max); }
        public void Tick()
        {
            if(State.player<0||State.pendingEvent>=0||Territory(State.player)==0)return;
            State.month++;if(State.month>12){State.month=1;State.year++;}
            for(int i=0;i<State.nations.Count;i++)
            {
                if(Territory(i)==0)continue;
                var ruler=State.nations[i];ruler.Gold+=Money(Income(i));
                if(ruler.Gold<Money(0)){ruler.Gold=Money(0);foreach(var region in State.nations.Where(n=>n.owner==i))region.troops=Math.Max(1,region.troops-1);}
                if(i==State.player)continue;
                var home=State.nations.First(n=>n.owner==i);
                if(ruler.Gold>=Money(90)){ruler.Gold-=Money(70);if(home.troops<12)home.troops+=4;else home.farms++;}
                foreach(int j in home.neighbors){var enemy=State.nations[j];if(enemy.owner==i)continue;
                    if(AtWar(i,enemy.owner)&&home.troops>enemy.troops+enemy.forts*4+2){Resolve(State.nations.IndexOf(home),j);break;}
                    if(State.month%6==0&&home.troops>enemy.troops*2&&!AtWar(i,enemy.owner))State.wars.Add(WarKey(i,enemy.owner));
                }
            }
            foreach(var n in State.nations)n.population+=2;
            if(State.month%4==0)State.pendingEvent=Random(3);
            Changed(Territory(State.player)==0?"你的国家已失去全部领土，本局结束。":"月度结算完成。关注税收与军费平衡。");
        }
        public void ChooseEvent(bool invest)
        {
            if(State.pendingEvent<0||State.player<0)return;var p=State.nations[State.player];
            if(invest&&p.Gold<Money(40)){Changed("国库不足 40 金。");return;}
            if(invest){p.Gold-=Money(40);var region=State.nations.First(n=>n.owner==State.player);if(State.pendingEvent==0)region.farms++;else if(State.pendingEvent==1)region.population+=30;else region.troops+=5;}
            else p.Gold+=Money(15);
            State.pendingEvent=-1;Changed(invest?"投入已经落实，国家获得长期收益。":"暂缓投入，国库保留了额外收入。");
        }
        public static bool Validate(Campaign s)
        {
            if(s==null||s.version!=1||s.nations==null||s.nations.Count!=28||s.wars==null||s.player<0||s.player>=28||s.month<1||s.month>12||s.year<2136||s.pendingEvent< -1||s.pendingEvent>2)return false;
            var expected=Create();
            for(int i=0;i<28;i++){var n=s.nations[i];if(n==null||n.id!=expected.nations[i].id||n.owner<0||n.owner>=28||n.troops<0||n.troops>1000000||n.treasury<0||n.farms<0||n.forts<0||n.population<0)return false;
                n.neighbors=expected.nations[i].neighbors;n.x=expected.nations[i].x;n.y=expected.nations[i].y;n.name=expected.nations[i].name;n.capital=expected.nations[i].capital;}
            return true;
        }
        public static Campaign Create()
        {
            var s=new Campaign();
            string[] rows={
                "W01|诺德维恩|维恩堡|220|120","W02|瓦尔瑟|瑟兰|205|192","W03|埃斯缇尔|缇尔城|269|169","W04|阿尔维昂|洛恩达|349|133",
                "E01|维斯卡恩|卡恩都|589|106","E02|奥瑟兰|瑟维亚|681|83","E03|诺尔瓦克|寒湾|721|33","E04|赫尔维克|赫林|727|57","E05|伊斯兰德尔|伊斯港|751|51","E06|卡尔登|登瓦|733|75","E07|梅瑞西亚|梅瑞尔|757|72","E08|塞维伦|维伦城|744|96","E09|塔尔维斯|塔尔门|664|145","E10|埃伦萨|萨兰|711|136","E11|洛萨维亚|洛萨|756|146","E12|维拉辛|辛海|789|177","E13|多尔卡斯|卡斯塔|724|177","E14|布雷恩|雷恩堡|678|185","E15|奥斯缇亚|缇亚|696|197","E16|佩尔萨|佩兰|711|216","E17|萨兰提尔|兰提|745|241",
                "C01|艾瑞汀|瑞汀|439|362","C02|塞洛恩|塞洛港|485|373","C03|马尔维亚|马尔登|416|408","C04|伊瑟拉|瑟拉湾|454|437",
                "S01|凯尔莫|凯尔关|829|333","S02|扎维伦|扎兰|875|368","S03|苏兰德|南望城|841|448"};
            foreach(string row in rows){var v=row.Split('|');int i=s.nations.Count;s.nations.Add(new Nation{id=v[0],name=v[1],capital=v[2],x=int.Parse(v[3])/925f,y=int.Parse(v[4])/602f,owner=i});}
            // Provisional land links. Explicit data, never connect across oceans by distance.
            string[] links={"0,1","0,2","0,3","1,2","2,3","4,5","4,12","4,17","5,6","5,7","5,12","5,13","6,7","6,8","7,8","7,9","8,10","9,10","9,11","10,11","11,13","11,14","12,13","12,17","13,14","13,16","14,15","14,16","15,16","15,20","16,18","16,19","16,20","17,18","18,19","19,20","21,22","21,23","22,24","23,24","25,26","25,27","26,27"};
            foreach(var link in links){var v=link.Split(',');int a=int.Parse(v[0]),b=int.Parse(v[1]);s.nations[a].neighbors.Add(b);s.nations[b].neighbors.Add(a);}return s;
        }
    }
}
