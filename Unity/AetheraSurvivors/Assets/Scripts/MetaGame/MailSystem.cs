// ============================================================
// 文件名：MailSystem.cs
// 功能描述：邮件系统 — 邮件列表/详情/附件领取/过期删除/已读标记
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #267
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>邮件数据</summary>
    [Serializable]
    public class MailData
    {
        public string MailId;
        public string Title;
        public string Content;
        public string SenderName;
        public long SendTime; // Unix时间戳
        public long ExpireTime; // 过期时间
        public bool IsRead;
        public bool AttachmentClaimed;
        public string AttachmentType; // "gold", "diamonds", "stamina", "hero_fragment"
        public int AttachmentAmount;
        public string AttachmentHeroId;
    }

    /// <summary>邮件存档</summary>
    [Serializable]
    public class MailSaveState
    {
        public List<MailData> Mails;
    }

    /// <summary>
    /// 邮件系统管理器
    /// </summary>
    public class MailSystem : Singleton<MailSystem>
    {
        private List<MailData> _mails;

        protected override void OnInit()
        {
            _mails = new List<MailData>();
            LoadMails();
            CleanExpiredMails();

            // 添加一些默认系统邮件
            if (_mails.Count == 0)
            {
                SendSystemMail("欢迎来到AetheraSurvivors！",
                    "亲爱的指挥官，欢迎加入以太守护者的行列！这里有一份新手礼包送给你。",
                    "gold", 1000);
                SendSystemMail("签到提醒",
                    "每日签到可获得丰厚奖励，连续签到7天还有额外惊喜！",
                    "diamonds", 50);
            }

            Debug.Log($"[MailSystem] 初始化完成, 邮件数: {_mails.Count}");
        }

        protected override void OnDispose()
        {
            SaveMails();
        }

        // ========== 公共方法 ==========

        /// <summary>获取所有邮件</summary>
        public List<MailData> GetAllMails() => _mails;

        /// <summary>获取未读邮件数</summary>
        public int GetUnreadCount()
        {
            int count = 0;
            for (int i = 0; i < _mails.Count; i++)
            {
                if (!_mails[i].IsRead) count++;
            }
            return count;
        }

        /// <summary>读取邮件</summary>
        public MailData ReadMail(string mailId)
        {
            var mail = FindMail(mailId);
            if (mail == null) return null;

            if (!mail.IsRead)
            {
                mail.IsRead = true;
                SaveMails();

                // 更新红点
                if (RedDotManager.HasInstance)
                {
                    RedDotManager.Instance.SetRedDot("mail_unread", GetUnreadCount() > 0, GetUnreadCount());
                }
            }

            return mail;
        }

        /// <summary>领取邮件附件</summary>
        public bool ClaimAttachment(string mailId)
        {
            var mail = FindMail(mailId);
            if (mail == null || mail.AttachmentClaimed) return false;
            if (string.IsNullOrEmpty(mail.AttachmentType)) return false;

            // 发放附件奖励
            DeliverReward(mail.AttachmentType, mail.AttachmentAmount, mail.AttachmentHeroId);

            mail.AttachmentClaimed = true;
            mail.IsRead = true;
            SaveMails();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new MailAttachmentClaimedEvent { MailId = mailId });
            }

            return true;
        }

        /// <summary>一键领取所有附件</summary>
        public int ClaimAllAttachments()
        {
            int claimed = 0;
            for (int i = 0; i < _mails.Count; i++)
            {
                if (!_mails[i].AttachmentClaimed && !string.IsNullOrEmpty(_mails[i].AttachmentType))
                {
                    if (ClaimAttachment(_mails[i].MailId))
                        claimed++;
                }
            }
            return claimed;
        }

        /// <summary>删除邮件</summary>
        public void DeleteMail(string mailId)
        {
            for (int i = _mails.Count - 1; i >= 0; i--)
            {
                if (_mails[i].MailId == mailId)
                {
                    _mails.RemoveAt(i);
                    break;
                }
            }
            SaveMails();
        }

        /// <summary>发送系统邮件</summary>
        public void SendSystemMail(string title, string content,
            string attachmentType = null, int attachmentAmount = 0, string heroId = null)
        {
            var mail = new MailData
            {
                MailId = Guid.NewGuid().ToString("N").Substring(0, 8),
                Title = title,
                Content = content,
                SenderName = "系统",
                SendTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpireTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30 * 86400, // 30天过期
                IsRead = false,
                AttachmentClaimed = false,
                AttachmentType = attachmentType,
                AttachmentAmount = attachmentAmount,
                AttachmentHeroId = heroId
            };

            _mails.Insert(0, mail); // 新邮件在最前面
            SaveMails();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new NewMailEvent { UnreadCount = GetUnreadCount() });
            }

            if (RedDotManager.HasInstance)
            {
                RedDotManager.Instance.SetRedDot("mail_unread", true, GetUnreadCount());
            }
        }

        // ========== 私有方法 ==========

        private MailData FindMail(string mailId)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].MailId == mailId) return _mails[i];
            }
            return null;
        }

        private void CleanExpiredMails()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = _mails.Count - 1; i >= 0; i--)
            {
                if (_mails[i].ExpireTime > 0 && _mails[i].ExpireTime < now)
                {
                    _mails.RemoveAt(i);
                }
            }
        }

        private void DeliverReward(string type, int amount, string heroId)
        {
            if (!Data.PlayerDataManager.HasInstance) return;
            switch (type)
            {
                case "gold": Data.PlayerDataManager.Instance.AddGold(amount); break;
                case "diamonds": Data.PlayerDataManager.Instance.AddDiamonds(amount); break;
                case "stamina":
                    var data = Data.PlayerDataManager.Instance.Data;
                    data.Stamina = Mathf.Min(data.Stamina + amount, data.MaxStamina * 2);
                    Data.PlayerDataManager.Instance.MarkDirty();
                    break;
                case "hero_fragment":
                    if (!string.IsNullOrEmpty(heroId))
                        HeroSystem.Instance.AddFragments(heroId, amount);
                    break;
            }
        }

        private void LoadMails()
        {
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<MailSaveState>("mail_state");
                if (state?.Mails != null)
                    _mails = state.Mails;
            }
        }

        private void SaveMails()
        {
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.Save("mail_state", new MailSaveState { Mails = _mails });
            }
        }
    }
}
