using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 整合版在原版模组列表里本 Mod 那一行加的“选项”按钮，以及按下后弹出的系统开关对话框。
    ///
    /// 按钮复制同一行原版的“浏览／订阅页”按钮，外观与原版一致；对话框直接用原版
    /// <see cref="InfoDialogScreen"/>（读档界面、DLC 开关等处使用的同一个弹窗），不引入 PLib。
    /// 开关写入 <see cref="SystemSwitches"/> 的配置文件，重启游戏后由 Mod 入口按开关装配补丁。
    /// </summary>
    internal static class SystemOptionsDialog
    {
        private const string ButtonName = "MaterialOverhaulOptionsButton";
        private const string Prefix = "STRINGS.MODOPTIONS.MATERIALOVERHAUL.";

        private static string ConfigPath => SystemSwitches.ConfigPath(KMod.Manager.GetDirectory());

        /// <summary>原版 BuildDisplay 每次都会销毁并重建全部行，所以每次重建后都要重新加按钮。</summary>
        public static void AddButton(ModsScreen screen, IEnumerable displayedMods)
        {
            List<KMod.Mod> mods = Global.Instance != null && Global.Instance.modManager != null
                ? Global.Instance.modManager.mods
                : null;
            if (screen == null || displayedMods == null || mods == null)
            {
                return;
            }

            foreach (object entry in displayedMods)
            {
                Traverse fields = Traverse.Create(entry);
                int index = fields.Field("mod_index").GetValue<int>();
                RectTransform row = fields.Field("rect_transform").GetValue<RectTransform>();
                if (row == null || index < 0 || index >= mods.Count)
                {
                    continue;
                }

                KMod.Mod mod = mods[index];
                if (mod == null || mod.staticID != ModInfo.StaticID || !mod.IsEnabledForActiveDlc())
                {
                    continue;
                }

                HierarchyReferences references = row.GetComponent<HierarchyReferences>();
                KButton manage = references != null ? references.GetReference<KButton>("ManageButton") : null;
                if (manage == null || manage.transform.parent == null)
                {
                    ModLog.WarnOnce("options-button-missing", "模组界面：找不到原版“浏览”按钮，系统开关“选项”按钮未添加。");
                    return;
                }

                Transform parent = manage.transform.parent;
                if (parent.Find(ButtonName) != null)
                {
                    return;
                }

                GameObject copy = Util.KInstantiateUI(manage.gameObject, parent.gameObject, true);
                copy.name = ButtonName;
                copy.transform.SetSiblingIndex(manage.transform.GetSiblingIndex());
                KButton button = copy.GetComponent<KButton>();
                button.ClearOnClick();
                button.isInteractable = true;
                LocText label = copy.GetComponentInChildren<LocText>();
                if (label != null)
                {
                    label.text = Strings.Get(Prefix + "BUTTON");
                }

                ToolTip tooltip = copy.GetComponent<ToolTip>();
                if (tooltip != null)
                {
                    tooltip.toolTip = Strings.Get(Prefix + "BUTTON_TOOLTIP");
                }

                GameObject dialogParent = screen.gameObject;
                button.onClick += () => Show(dialogParent);
                return;
            }
        }

        private static void Show(GameObject parent)
        {
            string problem;
            SystemSwitches pending = SystemSwitches.Load(ConfigPath, out problem);
            if (problem != null)
            {
                ModLog.Warn("系统开关文件读取失败，按全部开启显示：" + problem);
            }

            InfoDialogScreen dialog = Util.KInstantiateUI<InfoDialogScreen>(
                ScreenPrefabs.Instance.InfoDialogScreen.gameObject, parent);
            dialog.SetHeader(Strings.Get(Prefix + "TITLE"))
                .AddPlainText(Strings.Get(Prefix + "INTRO"));

            KButton toggleTemplate = dialog.GetPrimaryButtonPrefab().GetComponent<KButton>();
            foreach (ModSystem system in SystemSwitches.Systems)
            {
                ModSystem current = system;
                KButton toggle;
                dialog.AddUI(toggleTemplate, out toggle);
                LocText label = toggle.GetComponentInChildren<LocText>();
                ToolTip tooltip = toggle.GetComponent<ToolTip>();
                if (tooltip != null)
                {
                    tooltip.toolTip = Strings.Get(Prefix + "SYSTEMS." + SystemToken(current) + "_TOOLTIP");
                }

                System.Action refresh = () =>
                {
                    if (label != null)
                    {
                        label.text = ToggleText(current, pending);
                    }
                };
                refresh();
                toggle.onClick += () =>
                {
                    pending = pending.With(current, !pending.IsEnabled(current));
                    refresh();
                };
            }

            dialog.AddPlainText(Strings.Get(Prefix + "WARNING"))
                .AddDefaultCancel()
                .AddOption(Strings.Get(Prefix + "SAVE"), screen =>
                {
                    string error;
                    if (!SystemSwitches.Save(ConfigPath, pending, out error))
                    {
                        ModLog.Error("系统开关保存失败：" + error);
                        KMod.Manager.Dialog(parent, Strings.Get(Prefix + "TITLE"),
                            string.Format(Strings.Get(Prefix + "SAVE_FAILED"), error), null, () => { });
                        return;
                    }

                    ModLog.Info("系统开关已保存（重启后生效）：" + pending.Describe() + "；文件：" + ConfigPath);
                    screen.Deactivate();
                    if (!pending.SameAs(SystemSwitches.Active))
                    {
                        KMod.Manager.Dialog(parent,
                            Strings.Get(Prefix + "RESTART_TITLE"),
                            Strings.Get(Prefix + "RESTART_TEXT"),
                            Strings.Get(Prefix + "RESTART_NOW"),
                            App.instance.Restart,
                            Strings.Get(Prefix + "RESTART_LATER"),
                            // 原版确认框在取消回调为空时会隐藏取消按钮，所以这里给一个空回调。
                            () => { });
                    }
                }, rightSide: true)
                .Activate();
        }

        private static string ToggleText(ModSystem system, SystemSwitches pending)
        {
            bool enabled = pending.IsEnabled(system);
            string name = Strings.Get(Prefix + "SYSTEMS." + SystemToken(system));
            string text = string.Format(Strings.Get(Prefix + (enabled ? "STATE_ON" : "STATE_OFF")), name);
            if (enabled != SystemSwitches.Active.IsEnabled(system))
            {
                text += Strings.Get(Prefix + "PENDING");
            }

            return text;
        }

        private static string SystemToken(ModSystem system)
        {
            return SystemSwitches.Key(system).ToUpperInvariant();
        }
    }
}
