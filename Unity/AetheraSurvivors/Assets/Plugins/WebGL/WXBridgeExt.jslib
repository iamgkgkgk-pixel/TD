// WXBridgeExt.jslib — 微信小游戏扩展桥接文件
// 
// 配合 WXBridgeExtended.cs 使用
// 封装 wx.login / wx.checkSession / wx.getUserInfo / wx.onShow / wx.onHide
//       wx.shareAppMessage / wx.reportEvent / wx.setStorageSync / wx.getStorageSync
//       wx.showToast / wx.showModal / wx.setClipboardData / wx.vibrateShort / wx.vibrateLong
//
// 对应交互：阶段二 #62
// ============================================================

mergeInto(LibraryManager.library, {

    // ========================================
    // wx.login — 微信登录
    // ========================================
    WXEXT_Login: function(callbackObjPtr, successMethodPtr, failMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var successMethod = UTF8ToString(successMethodPtr);
        var failMethod = UTF8ToString(failMethodPtr);

        if (typeof wx !== 'undefined' && wx.login) {
            wx.login({
                success: function(res) {
                    var data = JSON.stringify({ code: res.code, errMsg: res.errMsg || 'login:ok' });
                    GameGlobal.unityNamespace.SendMessage(callbackObj, successMethod, data);
                },
                fail: function(err) {
                    var data = JSON.stringify({ code: '', errMsg: err.errMsg || 'login:fail' });
                    GameGlobal.unityNamespace.SendMessage(callbackObj, failMethod, data);
                }
            });
        } else {
            var mock = JSON.stringify({ code: 'NON_WX_ENV', errMsg: 'login:fail:not_wx' });
            GameGlobal.unityNamespace.SendMessage(callbackObj, failMethod, mock);
        }
    },

    // ========================================
    // wx.checkSession — 检查登录态
    // ========================================
    WXEXT_CheckSession: function(callbackObjPtr, successMethodPtr, failMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var successMethod = UTF8ToString(successMethodPtr);
        var failMethod = UTF8ToString(failMethodPtr);

        if (typeof wx !== 'undefined' && wx.checkSession) {
            wx.checkSession({
                success: function() {
                    GameGlobal.unityNamespace.SendMessage(callbackObj, successMethod, 'ok');
                },
                fail: function() {
                    GameGlobal.unityNamespace.SendMessage(callbackObj, failMethod, 'expired');
                }
            });
        } else {
            GameGlobal.unityNamespace.SendMessage(callbackObj, successMethod, 'ok');
        }
    },

    // ========================================
    // wx.getUserInfo — 获取用户信息
    // ========================================
    WXEXT_GetUserInfo: function(callbackObjPtr, successMethodPtr, failMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var successMethod = UTF8ToString(successMethodPtr);
        var failMethod = UTF8ToString(failMethodPtr);

        if (typeof wx !== 'undefined' && wx.getUserInfo) {
            wx.getUserInfo({
                success: function(res) {
                    var data = JSON.stringify(res.userInfo || {});
                    GameGlobal.unityNamespace.SendMessage(callbackObj, successMethod, data);
                },
                fail: function(err) {
                    GameGlobal.unityNamespace.SendMessage(callbackObj, failMethod, 
                        err.errMsg || 'getUserInfo:fail');
                }
            });
        } else {
            GameGlobal.unityNamespace.SendMessage(callbackObj, failMethod, 'not_wx_env');
        }
    },

    // ========================================
    // wx.getSystemInfoSync — 同步获取系统信息
    // ========================================
    WXEXT_GetSystemInfoSync: function() {
        var result;
        if (typeof wx !== 'undefined' && wx.getSystemInfoSync) {
            result = JSON.stringify(wx.getSystemInfoSync());
        } else {
            result = JSON.stringify({
                brand: 'Browser', model: 'Web', system: navigator.platform,
                platform: 'web', SDKVersion: 'N/A',
                screenWidth: window.innerWidth, screenHeight: window.innerHeight,
                windowWidth: window.innerWidth, windowHeight: window.innerHeight,
                pixelRatio: window.devicePixelRatio || 1, benchmarkLevel: -1
            });
        }
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    // ========================================
    // wx.getLaunchOptionsSync — 获取启动参数
    // ========================================
    WXEXT_GetLaunchOptions: function(callbackObjPtr, callbackMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        var data = '{}';
        if (typeof wx !== 'undefined' && wx.getLaunchOptionsSync) {
            var opts = wx.getLaunchOptionsSync();
            data = JSON.stringify({
                scene: opts.scene || 0,
                query: opts.query ? JSON.stringify(opts.query) : '',
                referrerInfo: opts.referrerInfo ? JSON.stringify(opts.referrerInfo) : ''
            });
        }
        GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
    },

    // ========================================
    // wx.onShow / wx.onHide — 前后台切换
    // ========================================
    WXEXT_RegisterOnShow: function(callbackObjPtr, callbackMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        if (typeof wx !== 'undefined' && wx.onShow) {
            wx.onShow(function(res) {
                var data = JSON.stringify({
                    scene: res.scene || 0,
                    query: res.query ? JSON.stringify(res.query) : ''
                });
                GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
            });
        }
    },

    WXEXT_RegisterOnHide: function(callbackObjPtr, callbackMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        if (typeof wx !== 'undefined' && wx.onHide) {
            wx.onHide(function() {
                GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, '');
            });
        }
    },

    // ========================================
    // wx.shareAppMessage — 分享
    // ========================================
    WXEXT_ShareAppMessage: function(titlePtr, imageUrlPtr, queryPtr) {
        var title = UTF8ToString(titlePtr);
        var imageUrl = UTF8ToString(imageUrlPtr);
        var query = UTF8ToString(queryPtr);

        if (typeof wx !== 'undefined' && wx.shareAppMessage) {
            wx.shareAppMessage({
                title: title,
                imageUrl: imageUrl || '',
                query: query || ''
            });
        }
    },

    WXEXT_ShowShareMenu: function() {
        if (typeof wx !== 'undefined' && wx.showShareMenu) {
            wx.showShareMenu({
                withShareTicket: true,
                menus: ['shareAppMessage', 'shareTimeline']
            });
        }
    },

    // ========================================
    // wx.reportEvent — 数据上报
    // ========================================
    WXEXT_ReportEvent: function(eventNamePtr, paramsJsonPtr) {
        var eventName = UTF8ToString(eventNamePtr);
        var paramsJson = UTF8ToString(paramsJsonPtr);

        if (typeof wx !== 'undefined' && wx.reportEvent) {
            try {
                var params = paramsJson ? JSON.parse(paramsJson) : {};
                wx.reportEvent(eventName, params);
            } catch (e) {
                console.warn('[WXBridgeExt] reportEvent error:', e);
            }
        }
    },

    // ========================================
    // wx.setStorageSync / getStorageSync / removeStorageSync
    // ========================================
    WXEXT_SetStorageSync: function(keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);
        if (typeof wx !== 'undefined' && wx.setStorageSync) {
            try { wx.setStorageSync(key, value); }
            catch (e) { console.error('[WXBridgeExt] setStorage error:', e); }
        }
    },

    WXEXT_GetStorageSync: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        var result = '';
        if (typeof wx !== 'undefined' && wx.getStorageSync) {
            try { result = wx.getStorageSync(key) || ''; }
            catch (e) { console.error('[WXBridgeExt] getStorage error:', e); }
        }
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    WXEXT_RemoveStorageSync: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        if (typeof wx !== 'undefined' && wx.removeStorageSync) {
            try { wx.removeStorageSync(key); }
            catch (e) { console.error('[WXBridgeExt] removeStorage error:', e); }
        }
    },

    // ========================================
    // wx.showToast / wx.showModal
    // ========================================
    WXEXT_ShowToast: function(titlePtr, iconPtr, duration) {
        var title = UTF8ToString(titlePtr);
        var icon = UTF8ToString(iconPtr);
        if (typeof wx !== 'undefined' && wx.showToast) {
            wx.showToast({ title: title, icon: icon || 'none', duration: duration || 1500 });
        }
    },

    WXEXT_ShowModal: function(titlePtr, contentPtr, callbackObjPtr, callbackMethodPtr) {
        var title = UTF8ToString(titlePtr);
        var content = UTF8ToString(contentPtr);
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        if (typeof wx !== 'undefined' && wx.showModal) {
            wx.showModal({
                title: title,
                content: content,
                success: function(res) {
                    var result = res.confirm ? 'confirm' : 'cancel';
                    GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, result);
                }
            });
        } else {
            var result = confirm(title + '\n' + content) ? 'confirm' : 'cancel';
            GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, result);
        }
    },

    // ========================================
    // wx.setClipboardData
    // ========================================
    WXEXT_SetClipboardData: function(dataPtr) {
        var data = UTF8ToString(dataPtr);
        if (typeof wx !== 'undefined' && wx.setClipboardData) {
            wx.setClipboardData({ data: data });
        } else if (navigator.clipboard) {
            navigator.clipboard.writeText(data);
        }
    },

    // ========================================
    // wx.vibrateShort / wx.vibrateLong
    // ========================================
    WXEXT_Vibrate: function(isLong) {
        if (typeof wx !== 'undefined') {
            if (isLong && wx.vibrateLong) {
                wx.vibrateLong();
            } else if (!isLong && wx.vibrateShort) {
                wx.vibrateShort({ type: 'medium' });
            }
        }
    }
});
