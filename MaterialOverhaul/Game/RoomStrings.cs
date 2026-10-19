namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间系统的本地化键树。键名前缀为 <c>STRINGS.ROOMS.MATERIALOVERHAUL</c>，
    /// 由 <c>LocString.CreateLocStringKeys(typeof(RoomStrings.ROOMS), "STRINGS.")</c> 建立。
    ///
    /// 字段默认值直接给中文：即使没有 .po，界面也能显示正确文本。
    /// 描述只写已实现的构成与环境条件；效果尚未接入，因此不在文本里承诺效果。
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
                    public static LocString DESC = "一个专门的淋浴空间。";
                    public static LocString TOOLTIP = "需要至少一个淋浴间、没有工业机械，大小 12–64 格。";
                }

                public static class ARTIFICIAL_PARK
                {
                    public static LocString NAME = "人造公园";
                    public static LocString DESC = "在室内布置绿植与公园标识形成的休憩空间。";
                    public static LocString TOOLTIP = "需要至少 4 株植物、一个公园标识、没有工业机械，大小 12–64 格。";
                }

                public static class INDUSTRIAL_AREA
                {
                    public static LocString NAME = "工业区";
                    public static LocString DESC = "集中布置工业生产设备的大型房间。";
                    public static LocString TOOLTIP = "需要至少 1 栋工业建筑、1 个存储设备和 1 个光照设备，平均装饰度至少 +60，大小 32–1000 格。";
                }

                public static class MEDICAL_CENTER
                {
                    public static LocString NAME = "医疗中心";
                    public static LocString DESC = "配备完整医疗设施的隔离治疗区。";
                    public static LocString TOOLTIP = "需要分诊床、医务站、配药桌和病诊站各至少一个，以及厕所、餐桌和光照设备；平均装饰度至少 +80，平均温度 20–26 ℃，没有工业机械，大小 24–108 格。";
                }

                public static class DANCE_HALL
                {
                    public static LocString NAME = "舞厅";
                    public static LocString DESC = "以点唱机器人为中心的娱乐空间。";
                    public static LocString TOOLTIP = "需要至少一个点唱机器人，不能有其他高级娱乐建筑；至少一个装饰建筑，平均装饰度至少 +100，平均温度 20–26 ℃，没有工业机械，大小 12–96 格。";
                }

                public static class ARCADE_HALL
                {
                    public static LocString NAME = "街机厅";
                    public static LocString DESC = "以游戏机台为中心的娱乐空间。";
                    public static LocString TOOLTIP = "需要至少一个游戏机台，不能有其他高级娱乐建筑；至少一个装饰建筑，平均装饰度至少 +100，平均温度 20–26 ℃，没有工业机械，大小 12–96 格。";
                }

                public static class BEACH_PARADISE
                {
                    public static LocString NAME = "海滨乐园";
                    public static LocString DESC = "模拟海边氛围的娱乐空间。";
                    public static LocString TOOLTIP = "需要沙滩椅、机械冲浪板、光照设备和装饰建筑，不能有其他高级娱乐建筑；平均装饰度至少 +80，没有工业机械，大小 12–96 格。";
                }

                public static class GALLERY
                {
                    public static LocString NAME = "展览馆";
                    public static LocString DESC = "展示藏品与杰出艺术品的展馆。";
                    public static LocString TOOLTIP = "需要基座展示藏品、至少一件杰出艺术品、光照设备，不能有其他高级娱乐建筑；平均装饰度至少 +120，平均温度 20–26 ℃，没有工业机械，大小 18–96 格。";
                }

                public static class WIND_TUNNEL
                {
                    public static LocString NAME = "风洞训练室";
                    public static LocString DESC = "以垂直风洞为核心的训练空间。";
                    public static LocString TOOLTIP = "需要至少一个垂直风洞和装饰建筑，不能有其他高级娱乐建筑；平均装饰度至少 +80，平均温度 20–26 ℃，没有工业机械，大小 36–96 格。";
                }

                public static class SPA
                {
                    public static LocString NAME = "水疗室";
                    public static LocString DESC = "以桑拿浴室或热水浴缸为核心的放松空间。";
                    public static LocString TOOLTIP = "需要至少一个桑拿浴室或热水浴缸和装饰建筑，不能有其他高级娱乐建筑；平均装饰度至少 +80，平均温度 26–50 ℃，没有工业机械，大小 16–64 格。";
                }

                public static class TEA_ROOM
                {
                    public static LocString NAME = "茶歇室";
                    public static LocString DESC = "提供饮品与饮水的休息空间。";
                    public static LocString TOOLTIP = "需要至少一个饮品娱乐设施和一台饮水机，没有工业机械；平均装饰度至少 +100，平均温度 20–26 ℃，大小 12–64 格。";
                }

                public static class MISSION_CONTROL
                {
                    public static LocString NAME = "任务控制室";
                    public static LocString DESC = "集中布置航天指挥设施的科研空间。";
                    public static LocString TOOLTIP = "需要至少一个航天指挥站，不能有其他科学建筑；至少一个光照设备，平均装饰度至少 +60，没有工业机械，大小 12–64 格。";
                }

                public static class GEO_TUNER_LAB
                {
                    public static LocString NAME = "地质研究室";
                    public static LocString DESC = "围绕地质调谐仪布置的科研空间。";
                    public static LocString TOOLTIP = "需要至少一个地质调谐仪，不能有其他科学建筑；至少一个光照设备，平均装饰度至少 +60，没有工业机械，大小 12–64 格。";
                }

                public static class SPACEPORT
                {
                    public static LocString NAME = "航天发射场";
                    public static LocString DESC = "集中停放与发射火箭的开阔场地。";
                    public static LocString TOOLTIP = "需要至少一个火箭发射平台，不能有生活建筑，大小 64–2400 格。";
                }

                public static class CARGO_STATION
                {
                    public static LocString NAME = "星际货运站";
                    public static LocString DESC = "集中布置星际发射与货物拆包设施的房间。";
                    public static LocString TOOLTIP = "需要至少一个星际物流发射器、一个载货拆包器、一个信号播报器和一个信号接收器，大小 32–360 格。";
                }

                public static class DATA_CENTER
                {
                    public static LocString NAME = "数据机房";
                    public static LocString DESC = "为数据挖掘中心提供低温环境的房间。";
                    public static LocString TOOLTIP = "需要至少一个数据挖掘中心，平均温度低于 20 ℃，大小 12–96 格。";
                }

                /// <summary>房间条件文本模板。与 <c>RoomText</c> 的格式化一一对应。</summary>
                public static class CRITERIA
                {
                    public static LocString PRIMARY = "需要{0}";
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
                }

                /// <summary>房间详情里的平均环境行。</summary>
                public static class DETAILS
                {
                    public static LocString AVERAGE_DECOR = "平均装饰度：{0}";
                    public static LocString AVERAGE_TEMPERATURE = "平均温度：{0}";
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
                        public static LocString TOOLTIP = "最近洗过澡，压力缓解 −10%。";
                    }

                    public static class BANQUET_RELIEF
                    {
                        public static LocString NAME = "宴会余韵";
                        public static LocString TOOLTIP = "最近在宴会厅享用过餐点，压力缓解 −10%。";
                    }

                    public static class HOSPITAL_RECOVERY
                    {
                        public static LocString NAME = "医院护理";
                        public static LocString TOOLTIP = "正在医院接受治疗，生命值回复速度 +20%。";
                    }

                    public static class MEDICAL_CENTER_RECOVERY
                    {
                        public static LocString NAME = "医疗中心护理";
                        public static LocString TOOLTIP = "正在医疗中心接受治疗，生命值回复速度 +50%。";
                    }

                    public static class CAREGIVER_BOOST
                    {
                        public static LocString NAME = "护理员照料";
                        public static LocString TOOLTIP = "护理员正在照料，生命值回复速度再 +50%。";
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
                        public static LocString TOOLTIP = "最近在水疗室放松过，士气 +2，压力缓解 −50%。";
                    }

                    public static class TEA_ROOM_MORALE
                    {
                        public static LocString NAME = "茶歇时光";
                        public static LocString TOOLTIP = "最近在茶歇室休息过，士气 +2，压力缓解 −30%。";
                    }
                }
            }
        }
    }
}
