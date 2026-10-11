using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Orders;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 逐笔订单列表侧屏（V0.0.3 开发计划 §5.1）。
    ///
    /// 原版侧屏只有「按配方合计数量」的入口，同配方的多笔订单会被合并成一行，
    /// 无法逐笔查看、编辑或取消。这里给金属精炼器补一块按**玩家添加顺序**排列的列表：
    /// 每行显示配方、有限剩余／无限本轮进度、当前批次标记，并提供取消、加减数量、
    /// 切换无限与调整每轮额度的按钮。新增订单仍然走原版配方列表（追加到末尾，同配方可重复）。
    ///
    /// 界面完全由代码构建（不引入 PLib，也不依赖原版侧屏预制件的层级），
    /// 因此所有构建步骤都包在 try/catch 里：构建失败只留下可定位日志，不影响建筑本身运行。
    /// </summary>
    public class OrderListSideScreen : SideScreenContent
    {
        private const string KeyRoot = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.ORDER_LIST.";

        private MaterialOverhaulFab target;
        private RectTransform root;
        private LocText titleLabel;
        private LocText hintLabel;
        private LocText emptyLabel;
        private RectTransform rowContainer;
        private bool built;

        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>
        /// 建立侧屏预制件。预制件保持**非激活**：详情面板会用
        /// <c>Util.KInstantiateUI</c> 克隆它并挂到标签页下，界面在 <see cref="SetTarget"/> 时惰性构建，
        /// 避免在预制件阶段就依赖 Unity 生命周期顺序。
        /// </summary>
        public static OrderListSideScreen CreatePrefab()
        {
            try
            {
                GameObject go = new GameObject("MaterialOverhaulOrderListScreen", typeof(RectTransform));
                go.SetActive(false);
                return go.AddComponent<OrderListSideScreen>();
            }
            catch (Exception e)
            {
                ModLog.Error("建立订单列表侧屏预制件失败：" + e);
                return null;
            }
        }

        public override bool IsValidForTarget(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<MaterialOverhaulFab>() != null;
        }

        public override string GetTitle()
        {
            return Localized("TITLE", "材料革新：订单列表");
        }

        public override void SetTarget(GameObject newTarget)
        {
            try
            {
                target = newTarget != null ? newTarget.GetComponent<MaterialOverhaulFab>() : null;
                EnsureBuilt();
                RebuildRows();
            }
            catch (Exception e)
            {
                ModLog.Error("订单列表侧屏设置目标失败：" + e);
            }
        }

        public override void ClearTarget()
        {
            target = null;
        }

        /* ---- 界面构建 ---- */

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;

            root = transform as RectTransform;
            if (root == null)
            {
                ModLog.Error("订单列表侧屏缺少 RectTransform，界面未构建。");
                return;
            }

            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.padding = new RectOffset(6, 6, 6, 6);

            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleLabel = CreateLabel(root, 15f, false);
            titleLabel.text = GetTitle();

            hintLabel = CreateLabel(root, 12f, false);
            hintLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            hintLabel.text = string.Format(Localized("LIMIT_HINT", "无限额度范围 1–{0}。"), Balance.MaxInfiniteBatchQuota);

            emptyLabel = CreateLabel(root, 13f, false);
            emptyLabel.text = Localized("EMPTY", "还没有订单。用上方的配方列表添加。");

            GameObject container = new GameObject("Rows", typeof(RectTransform));
            rowContainer = container.GetComponent<RectTransform>();
            rowContainer.SetParent(root, false);
            VerticalLayoutGroup rowLayout = container.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 3f;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            ContentSizeFitter rowFitter = container.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RebuildRows()
        {
            if (!built)
            {
                return;
            }

            foreach (GameObject row in rows)
            {
                if (row != null)
                {
                    Util.KDestroyGameObject(row);
                }
            }

            rows.Clear();

            bool hasTarget = target != null;
            emptyLabel.gameObject.SetActive(hasTarget && target.Orders.Count == 0);
            rowContainer.gameObject.SetActive(hasTarget && target.Orders.Count > 0);

            if (!hasTarget)
            {
                return;
            }

            for (int i = 0; i < target.Orders.Count; i++)
            {
                OrderEntry entry = target.Orders.Orders[i];
                rows.Add(CreateRow(entry));
            }
        }

        private GameObject CreateRow(OrderEntry entry)
        {
            bool isCurrent = target != null && target.CurrentOrder != null && target.CurrentOrder.Id == entry.Id;

            GameObject row = new GameObject("Order#" + entry.Id, typeof(RectTransform));
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.SetParent(rowContainer, false);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            LocText marker = CreateLabel(rowRect, 13f, false, 18f);
            marker.text = Localized(isCurrent ? "ROW_CURRENT" : "ROW_OTHER", isCurrent ? "▶" : "　");

            string name = target != null ? target.RecipeDisplayName(entry.RecipeId) : entry.RecipeId;
            string text = entry.IsInfinite
                ? string.Format(Localized("ROW_INFINITE", "{0}　无限（本轮 {1}/{2}）"), name, entry.BatchesDoneThisRound, entry.BatchQuota)
                : string.Format(Localized("ROW_LIMITED", "{0}　剩余 {1} 批"), name, entry.Remaining);

            LocText label = CreateLabel(rowRect, 13f, false);
            label.text = text;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;

            int id = entry.Id;

            CreateButton(rowRect, Localized("BUTTON_MINUS", "−"), Localized("BUTTON_MINUS_TOOLTIP", "剩余批次数减 1。"), () =>
            {
                OrderEntry current = target != null ? target.Orders.FindById(id) : null;
                if (current == null)
                {
                    return;
                }

                if (current.IsInfinite)
                {
                    target.EditOrderQuota(id, current.BatchQuota - 1);
                }
                else
                {
                    target.EditOrderRemaining(id, current.Remaining - 1);
                }

                RebuildRows();
            });

            CreateButton(rowRect, Localized("BUTTON_PLUS", "+"), Localized("BUTTON_PLUS_TOOLTIP", "剩余批次数加 1。"), () =>
            {
                OrderEntry current = target != null ? target.Orders.FindById(id) : null;
                if (current == null)
                {
                    return;
                }

                if (current.IsInfinite)
                {
                    target.EditOrderQuota(id, current.BatchQuota + 1);
                }
                else
                {
                    target.EditOrderRemaining(id, current.Remaining + 1);
                }

                RebuildRows();
            });

            CreateButton(rowRect, Localized("BUTTON_INFINITE", "∞"), Localized("BUTTON_INFINITE_TOOLTIP", "改为无限订单。"), () =>
            {
                OrderEntry current = target != null ? target.Orders.FindById(id) : null;
                if (current == null)
                {
                    return;
                }

                if (current.IsInfinite)
                {
                    target.EditOrderInfinite(id, false, current.BatchQuota);
                }
                else
                {
                    target.EditOrderInfinite(id, true, Balance.DefaultInfiniteBatchQuota);
                }

                RebuildRows();
            });

            CreateButton(rowRect, Localized("BUTTON_REMOVE", "取消"), Localized("BUTTON_REMOVE_TOOLTIP", "取消这一笔订单。"), () =>
            {
                if (target != null)
                {
                    target.CancelOrder(id);
                }

                RebuildRows();
            });

            return row;
        }


        private LocText CreateLabel(Transform parent, float fontSize, bool centered, float fixedWidth = 0f)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));

            // 与 PLib 的 AddLocText 同一套做法：**先禁用再添加** LocText，
            // 让 Awake 在已经写好 key 与 textStyleSetting 的状态下执行，
            // 样式（含字体）才会正确套用；否则界面会没有字体。
            bool active = go.activeSelf;
            go.SetActive(false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            LocText label = go.AddComponent<LocText>();
            label.key = string.Empty;
            label.textStyleSetting = Style;
            go.SetActive(active);

            if (label.textStyleSetting != null)
            {
                label.ApplySettings();
            }

            label.fontSize = fontSize;
            label.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = string.Empty;

            if (fixedWidth > 0f)
            {
                LayoutElement layout = go.AddComponent<LayoutElement>();
                layout.preferredWidth = fixedWidth;
                layout.minWidth = fixedWidth;
            }

            return label;
        }

        /// <summary>
        /// 文本样式。取游戏自带的提示正文样式，保证字体与颜色与其它界面一致；
        /// 取不到时返回 null（调用方会跳过样式设置，界面仍可显示）。
        /// </summary>
        private static TextStyleSetting Style
        {
            get
            {
                if (labelStyle != null)
                {
                    return labelStyle;
                }

                if (ToolTipScreen.Instance != null)
                {
                    labelStyle = ToolTipScreen.Instance.defaultTooltipBodyStyle;
                }

                return labelStyle;
            }
        }

        private static TextStyleSetting labelStyle;

        private void CreateButton(Transform parent, string text, string tooltip, System.Action onClick)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.35f, 0.35f, 0.38f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 34f;
            layout.minWidth = 34f;
            layout.preferredHeight = 20f;
            layout.minHeight = 20f;

            LocText label = CreateLabel(rect, 12f, true);
            label.text = text;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // 说明文本只用于本地化键的集中管理：本版按钮不挂 ToolTip 组件
            // （ToolTip 不在已核实的程序集里，无法在不做游戏内验证的前提下保证签名）。
        }

        private static string Localized(string keySuffix, string fallback)
        {
            return MaterialOverhaulFab.LocalizedOr(KeyRoot + keySuffix, fallback);
        }
    }
}
