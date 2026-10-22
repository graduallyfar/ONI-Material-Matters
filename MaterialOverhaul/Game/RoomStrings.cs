namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间系统的本地化键树。键名前缀为 <c>STRINGS.ROOMS.MATERIALOVERHAUL</c>，
    /// 由 <c>LocString.CreateLocStringKeys(typeof(RoomStrings.ROOMS), "STRINGS.")</c> 建立。
    ///
    /// 字段默认值直接给中文：即使没有 .po，界面也能显示正确文本。
    /// 房间的 DESC／EFFECT／TOOLTIP 与原版房间同一写法：说明、图例简述、一句效果提示；
    /// 构成条件由条件列表显示，不在提示里重复。
    /// </summary>
    public static class RoomStrings
    {
        public static class ROOMS
        {
            public static class MATERIALOVERHAUL
            {
                public static class SHOWER_ROOM
                {
                    public static LocString NAME = "淋浴间";
                    public static LocString DESC = "一个独立的淋浴空间。\n\n在淋浴间洗澡能缓解复制人的压力。";
                    public static LocString EFFECT = "- 压力缓解";
                    public static LocString TOOLTIP = "在淋浴间洗澡能缓解复制人的压力";
                }

                public static class ARTIFICIAL_PARK
                {
                    public static LocString NAME = "人造公园";
                    public static LocString DESC = "用绿植和公园标识布置的室内休憩空间。\n\n经过人造公园能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "经过人造公园能提升复制人的士气";
                }

                public static class INDUSTRIAL_AREA
                {
                    public static LocString NAME = "工业区";
                    public static LocString DESC = "集中布置工业设备的大型厂房。\n\n工业区内的工业建筑生产更快。";
                    public static LocString EFFECT = "- 生产加速";
                    public static LocString TOOLTIP = "工业区内的工业建筑生产更快";
                }

                public static class MEDICAL_CENTER
                {
                    public static LocString NAME = "医疗中心";
                    public static LocString DESC = "配齐各类医疗设施的大型医院。\n\n患病的复制人会优先送到这里，治疗期间生命回复更快。";
                    public static LocString EFFECT = "- 治疗加成";
                    public static LocString TOOLTIP = "患病的复制人优先送到这里，治疗期间生命回复更快";
                }

                public static class DANCE_HALL
                {
                    public static LocString NAME = "舞厅";
                    public static LocString DESC = "以点唱机器人为中心的娱乐空间。\n\n在舞厅放松能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "在舞厅放松能提升复制人的士气";
                }

                public static class ARCADE_HALL
                {
                    public static LocString NAME = "街机厅";
                    public static LocString DESC = "以游戏机台为中心的娱乐空间。\n\n在街机厅游玩能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "在街机厅游玩能提升复制人的士气";
                }

                public static class BEACH_PARADISE
                {
                    public static LocString NAME = "海滨乐园";
                    public static LocString DESC = "模拟海边氛围的娱乐空间。\n\n在海滨乐园放松能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "在海滨乐园放松能提升复制人的士气";
                }

                public static class GALLERY
                {
                    public static LocString NAME = "展览馆";
                    public static LocString DESC = "展示藏品与杰出艺术品的展馆。\n\n参观展览馆能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "参观展览馆能提升复制人的士气";
                }

                public static class WIND_TUNNEL
                {
                    public static LocString NAME = "风洞训练室";
                    public static LocString DESC = "以垂直风洞为核心的训练空间。\n\n在风洞训练室活动能提升复制人的士气。";
                    public static LocString EFFECT = "- 士气加成";
                    public static LocString TOOLTIP = "在风洞训练室活动能提升复制人的士气";
                }

                public static class SPA
                {
                    public static LocString NAME = "水疗室";
                    public static LocString DESC = "以桑拿浴室或热水浴缸为核心的放松空间。\n\n在水疗室放松能提升士气并缓解压力。";
                    public static LocString EFFECT = "- 士气加成、压力缓解";
                    public static LocString TOOLTIP = "在水疗室放松能提升士气并缓解压力";
                }

                public static class TEA_ROOM
                {
                    public static LocString NAME = "茶歇室";
                    public static LocString DESC = "提供饮品与饮水的休息空间。\n\n在茶歇室休息能提升士气并缓解压力。";
                    public static LocString EFFECT = "- 士气加成、压力缓解";
                    public static LocString TOOLTIP = "在茶歇室休息能提升士气并缓解压力";
                }

                public static class MISSION_CONTROL
                {
                    public static LocString NAME = "任务控制室";
                    public static LocString DESC = "集中布置航天指挥站的科研空间。\n\n这里的航天指挥站工作更快，助推的火箭飞得更快。";
                    public static LocString EFFECT = "- 火箭加速";
                    public static LocString TOOLTIP = "这里的航天指挥站工作更快，助推的火箭飞得更快";
                }

                public static class GEO_TUNER_LAB
                {
                    public static LocString NAME = "地质研究室";
                    public static LocString DESC = "围绕地质调谐仪布置的科研空间。\n\n这里的地质调谐仪研究更快，调谐效果更强。";
                    public static LocString EFFECT = "- 调谐加成";
                    public static LocString TOOLTIP = "这里的地质调谐仪研究更快，调谐效果更强";
                }

                public static class SPACEPORT
                {
                    public static LocString NAME = "航天发射场";
                    public static LocString DESC = "集中停放与发射火箭的开阔场地。\n\n从这里发射的火箭飞得更快、更省燃料。";
                    public static LocString EFFECT = "- 火箭加速、节省燃料";
                    public static LocString TOOLTIP = "从这里发射的火箭飞得更快、更省燃料";
                }

                public static class CARGO_STATION
                {
                    public static LocString NAME = "星际货运站";
                    public static LocString DESC = "集中布置星际物流设施的房间。\n\n这里的星际物流发射器发射更快、连发次数更多。";
                    public static LocString EFFECT = "- 物流加速";
                    public static LocString TOOLTIP = "这里的星际物流发射器发射更快、连发次数更多";
                }

                public static class DATA_CENTER
                {
                    public static LocString NAME = "数据机房";
                    public static LocString DESC = "为数据挖掘中心提供低温环境的房间。\n\n这里的数据挖掘中心生产更快。";
                    public static LocString EFFECT = "- 生产加速";
                    public static LocString TOOLTIP = "这里的数据挖掘中心生产更快";
                }

                /// <summary>
                /// 本 Mod 创建的六项娱乐偏好特质。工作口径（D13 未确认）：加入原版先天性特质池，
                /// 在对应房间内获得专用效果；同时拥有两项相关特质时取映射表第一项。
                /// </summary>
                public static class TRAITS
                {
                    public static class SUPERSTAR
                    {
                        public static LocString NAME = "超级明星";
                        public static LocString DESC = "需要在舞厅舒展天赋。";
                    }

                    public static class ESPORTS
                    {
                        public static LocString NAME = "电子竞技选手";
                        public static LocString DESC = "需要在街机厅舒展天赋。";
                    }

                    public static class BEACH
                    {
                        public static LocString NAME = "海滨达人";
                        public static LocString DESC = "需要模拟海边来放松。";
                    }

                    public static class ADVENTURER
                    {
                        public static LocString NAME = "冒险家";
                        public static LocString DESC = "需要机械冲浪板来展现天赋。";
                    }

                    public static class ARTIST
                    {
                        public static LocString NAME = "艺术家";
                        public static LocString DESC = "需要展览馆来放松自己。";
                    }

                    public static class ROCKET
                    {
                        public static LocString NAME = "火箭专家";
                        public static LocString DESC = "需要风洞来锻炼自己。";
                    }
                }

                /// <summary>房间条件文本模板。与 <c>RoomText</c> 的格式化一一对应。</summary>
                public static class CRITERIA
                {
                    public static LocString PRIMARY = "至少 1 个{0}";
                    public static LocString PRIMARY_OR = "或";
                    public static LocString MIN_SIZE = "房间至少 {0} 格";
                    public static LocString MAX_SIZE = "房间不超过 {0} 格";
                    public static LocString MIN_PLANTS = "至少 {0} 株植物";
                    public static LocString NO_INDUSTRIAL_MACHINERY = "无工业机械";
                    public static LocString INDUSTRIAL_BUILDING = "至少 1 栋工业建筑";
                    public static LocString STORAGE = "至少 1 个存储设备";
                    public static LocString LIGHT = "至少 1 个光照设备";
                    public static LocString TOILET = "至少 1 个厕所";
                    public static LocString DINING_TABLE = "至少 1 张餐桌";
                    public static LocString DECORATION = "至少 1 个装饰建筑";
                    public static LocString ORNAMENT = "基座展示藏品";
                    public static LocString MASTERPIECE = "至少 1 件杰出艺术品";
                    public static LocString MIN_DECOR = "房间平均装饰度至少 {0}";
                    public static LocString TEMP_RANGE = "房间平均温度 {0}–{1} ℃";
                    public static LocString TEMP_BELOW = "房间平均温度低于 {0} ℃";
                    public static LocString PREFAB = "至少 1 个{0}";
                    public static LocString ALL_PREFABS = "需要全部：{0}";
                    public static LocString NO_OTHER_ADVANCED_RECREATION = "无其他高级娱乐建筑";
                    public static LocString NO_OTHER_SCIENCE = "无其他科学建筑";
                    public static LocString NO_LIFE_BUILDING = "无生活建筑";
                    public static LocString UPGRADE_HEADER = "可升级为";
                }

                /// <summary>房间详情里的平均环境行。</summary>
                public static class DETAILS
                {
                    public static LocString AVERAGE_DECOR = "平均装饰度：{0}";
                    public static LocString AVERAGE_TEMPERATURE = "平均温度：{0}";
                    public static LocString PRODUCTION_BONUS = "工作效率：+{0}%";
                    public static LocString MISSION_CONTROL_BONUS = "火箭助推：速度 +{0}%";
                    public static LocString MISSION_CONTROL_RANGE = "，指挥范围 +{0} 格";
                    public static LocString GEOTUNER_LAB_BONUS = "地质调谐：喷发量 ×{0}，每座喷泉多接 {1} 台调谐仪";
                    public static LocString SPACEPORT_BONUS = "发射的火箭：速度 +{0}%，燃料消耗 −{1}%";
                    public static LocString CARGO_STATION_BONUS = "星际物流：载荷速度 +{0}%，每轮连发 {1} 次，冷却加快 {2}%";
                    public static LocString MEDICAL_CENTER_BONUS = "患者优先入院，可救治致命辐射病（会留下后遗症）";
                    public static LocString RADIATION_RESCUE = "可救治致命辐射病，但会留下后遗症";
                    public static LocString GEOTUNER_EXTRA_SLOT = "该喷泉已有 {0} 台调谐仪，地质研究室内最多 {1} 台。";
                }

                /// <summary>建筑与火箭状态栏里的房间加成条目。只写公开的基础加成，不写科学翻倍等隐藏机制。</summary>
                public static class STATUS
                {
                    public static class BUILDING_BONUS
                    {
                        public static LocString NAME = "{0}：{1}";
                        public static LocString TOOLTIP = "这个建筑位于{0}，获得加成：\n{1}";
                    }

                    public static class ROCKET_BONUS
                    {
                        public static LocString NAME = "房间加成：速度 +{0}%";
                        public static LocString TOOLTIP = "这枚火箭本次飞行获得房间加成：\n{0}";
                    }

                    public static class GEYSER_BONUS
                    {
                        public static LocString NAME = "地质研究室：喷发量 ×{0}";
                        public static LocString TOOLTIP = "有调谐仪在地质研究室中为这座喷泉广播，原版调谐之后喷发量再 ×{0}。";
                    }

                    public static LocString SEPARATOR = "，";
                    public static LocString PRODUCTION = "生产速度 +{0}%";
                    public static LocString WORK = "工作速度 +{0}%";
                    public static LocString MISSION_CONTROL = "助推的火箭速度 +{0}%";
                    public static LocString MISSION_CONTROL_RANGE = "指挥范围 +{0} 格";
                    public static LocString GEOTUNER = "喷发量 ×{0}，调谐仪上限 +{1}";
                    public static LocString SPACEPORT = "发射的火箭速度 +{0}%，燃料消耗 −{1}%";
                    public static LocString CARGO = "载荷速度 +{0}%，每轮连发 {1} 次，冷却加快 {2}%";
                    public static LocString RECOVERY = "患者生命回复 +{0}%";
                    public static LocString ROCKET_MISSION_CONTROL = "任务控制室助推：速度 +{0}%";
                    public static LocString ROCKET_SPACEPORT = "航天发射场发射：速度 +{0}%，燃料消耗 −{1}%";
                }

                /// <summary>
                /// 已实装房间效果的名称与提示。只登记真正会发放的效果；
                /// 压力、特质、生产效率等未接入项不在此虚构。
                /// </summary>
                public static class EFFECTS
                {
                    public static class ARTIFICIAL_PARK_MORALE
                    {
                        public static LocString NAME = "公园漫步";
                        public static LocString TOOLTIP = "最近经过人造公园，士气 +2。";
                    }

                    public static class SHOWER_RELIEF
                    {
                        public static LocString NAME = "清爽淋浴";
                        public static LocString TOOLTIP = "最近洗过澡，压力每周期 −10%。";
                    }

                    public static class BANQUET_RELIEF
                    {
                        public static LocString NAME = "宴会余韵";
                        public static LocString TOOLTIP = "最近在宴会厅享用过餐点，压力每周期 −10%。";
                    }

                    public static class HOSPITAL_RECOVERY
                    {
                        public static LocString NAME = "医院护理";
                        public static LocString TOOLTIP = "正在医院接受治疗，生命值回复速度 +25%。";
                    }

                    public static class MEDICAL_CENTER_RECOVERY
                    {
                        public static LocString NAME = "医疗中心护理";
                        public static LocString TOOLTIP = "正在医疗中心接受治疗，生命值回复速度 +50%。";
                    }

                    public static class CAREGIVER_BOOST
                    {
                        public static LocString NAME = "护理员照料";
                        public static LocString TOOLTIP = "护理员正在照料，生命值回复速度再 ×1.25。";
                    }

                    public static class TRAIT_SUPERSTAR
                    {
                        public static LocString NAME = "舞台焦点";
                        public static LocString TOOLTIP = "在舞厅舒展天赋：士气 +4、压力每周期 −20%、运动 +3。";
                    }

                    public static class TRAIT_ESPORTS
                    {
                        public static LocString NAME = "竞技状态";
                        public static LocString TOOLTIP = "在街机厅舒展天赋：士气 +4、压力每周期 −20%、机械 +3。";
                    }

                    public static class TRAIT_BEACH
                    {
                        public static LocString NAME = "海滨放松";
                        public static LocString TOOLTIP = "在海滨乐园放松：士气 +4、压力每周期 −20%、挖掘 +3。";
                    }

                    public static class TRAIT_ADVENTURER
                    {
                        public static LocString NAME = "冲浪冒险";
                        public static LocString TOOLTIP = "在海滨乐园冲浪：士气 +4、压力每周期 −20%、建造 +3。";
                    }

                    public static class TRAIT_ARTIST
                    {
                        public static LocString NAME = "艺术灵感";
                        public static LocString TOOLTIP = "在展览馆放松：士气 +4、压力每周期 −20%、艺术 +3。";
                    }

                    public static class TRAIT_ROCKET
                    {
                        public static LocString NAME = "风洞训练";
                        public static LocString TOOLTIP = "在风洞训练：士气 +4、压力每周期 −20%、驾驶 +3。";
                    }

                    public static class DANCE_HALL_MORALE
                    {
                        public static LocString NAME = "舞厅时光";
                        public static LocString TOOLTIP = "最近在舞厅放松过，士气 +2。";
                    }

                    public static class ARCADE_HALL_MORALE
                    {
                        public static LocString NAME = "街机厅时光";
                        public static LocString TOOLTIP = "最近在街机厅玩过，士气 +2。";
                    }

                    public static class BEACH_PARADISE_MORALE
                    {
                        public static LocString NAME = "海滨假期";
                        public static LocString TOOLTIP = "最近在海滨乐园休息过，士气 +2。";
                    }

                    public static class GALLERY_MORALE
                    {
                        public static LocString NAME = "艺术熏陶";
                        public static LocString TOOLTIP = "最近参观过展览馆，士气 +2。";
                    }

                    public static class WIND_TUNNEL_MORALE
                    {
                        public static LocString NAME = "风洞训练";
                        public static LocString TOOLTIP = "最近在风洞训练室活动过，士气 +2。";
                    }

                    public static class SPA_MORALE
                    {
                        public static LocString NAME = "水疗放松";
                        public static LocString TOOLTIP = "最近在水疗室放松过，士气 +2，压力每周期 −40%。";
                    }

                    public static class TEA_ROOM_MORALE
                    {
                        public static LocString NAME = "茶歇时光";
                        public static LocString TOOLTIP = "最近在茶歇室休息过，士气 +2，压力每周期 −30%。";
                    }

                    public static class RADIATION_SCAR
                    {
                        public static LocString NAME = "辐射后遗症";
                        public static LocString TOOLTIP = "曾在医疗中心从致命辐射病中获救，留下永久后遗症。";
                    }
                }
            }
        }
    }
}
