
// WXBridge.jslib — Unity WebGL ↔ 微信小游戏 JS 桥接文件
// 
// 本文件定义了 C# 通过 DllImport("__Internal") 调用的 JS 函数
// 必须放在 Assets/Plugins/WebGL/ 目录下才能被 Unity WebGL 编译器识别
//
// 注意：
// 1. 使用 minigame-unity-sdk 时，部分API可能已由SDK封装
//    本文件作为备用/教学用途，验证原生 JS 桥接是否畅通
// 2. 如果 SDK 已提供相同功能，可删除本文件中重复的函数

mergeInto(LibraryManager.library, {

    // ========================================
    // wx.login — 微信登录
    // ========================================
    WX_Login: function(callbackObjPtr, callbackMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);
        
        console.log('[WXBridge.jslib] 调用 wx.login...');
        
        if (typeof wx !== 'undefined' && wx.login) {
            wx.login({
                success: function(res) {
                    console.log('[WXBridge.jslib] wx.login 成功, code=' + res.code);
                    var data = JSON.stringify({
                        code: res.code,
                        errMsg: res.errMsg || 'login:ok'
                    });
                    if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                        GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
                    } else if (typeof unityInstance !== 'undefined') {
                        unityInstance.SendMessage(callbackObj, callbackMethod, data);
                    }
                },
                fail: function(err) {
                    console.error('[WXBridge.jslib] wx.login 失败:', err);
                    var data = JSON.stringify({
                        code: '',
                        errMsg: err.errMsg || 'login:fail'
                    });
                    if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                        GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
                    } else if (typeof unityInstance !== 'undefined') {
                        unityInstance.SendMessage(callbackObj, callbackMethod, data);
                    }
                }
            });
        } else {
            console.warn('[WXBridge.jslib] wx 对象不存在（非微信环境）');
            var mockData = JSON.stringify({
                code: 'NON_WX_ENV',
                errMsg: 'login:fail:not in wechat'
            });
            if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, mockData);
            } else if (typeof unityInstance !== 'undefined') {
                unityInstance.SendMessage(callbackObj, callbackMethod, mockData);
            }
        }
    },

    // ========================================
    // wx.getSystemInfo — 获取系统信息（异步）
    // ========================================
    WX_GetSystemInfo: function(callbackObjPtr, callbackMethodPtr) {
        var callbackObj = UTF8ToString(callbackObjPtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);
        
        console.log('[WXBridge.jslib] 调用 wx.getSystemInfo...');
        
        if (typeof wx !== 'undefined' && wx.getSystemInfo) {
            wx.getSystemInfo({
                success: function(res) {
                    console.log('[WXBridge.jslib] wx.getSystemInfo 成功:', res);
                    var data = JSON.stringify({
                        brand: res.brand || '',
                        model: res.model || '',
                        system: res.system || '',
                        platform: res.platform || '',
                        SDKVersion: res.SDKVersion || '',
                        screenWidth: res.screenWidth || 0,
                        screenHeight: res.screenHeight || 0,
                        windowWidth: res.windowWidth || 0,
                        windowHeight: res.windowHeight || 0,
                        pixelRatio: res.pixelRatio || 1,
                        benchmarkLevel: res.benchmarkLevel || -1
                    });
                    if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                        GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
                    } else if (typeof unityInstance !== 'undefined') {
                        unityInstance.SendMessage(callbackObj, callbackMethod, data);
                    }
                },
                fail: function(err) {
                    console.error('[WXBridge.jslib] wx.getSystemInfo 失败:', err);
                    var data = JSON.stringify({ error: err.errMsg || 'getSystemInfo:fail' });
                    if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                        GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, data);
                    } else if (typeof unityInstance !== 'undefined') {
                        unityInstance.SendMessage(callbackObj, callbackMethod, data);
                    }
                }
            });
        } else {
            console.warn('[WXBridge.jslib] wx 对象不存在（非微信环境）');
            var mockData = JSON.stringify({
                brand: 'Browser',
                model: navigator.userAgent.substring(0, 30),
                system: navigator.platform,
                platform: 'web',
                SDKVersion: 'N/A',
                screenWidth: window.innerWidth,
                screenHeight: window.innerHeight,
                windowWidth: window.innerWidth,
                windowHeight: window.innerHeight,
                pixelRatio: window.devicePixelRatio || 1,
                benchmarkLevel: -1
            });
            if (typeof GameGlobal !== 'undefined' && GameGlobal.unityNamespace) {
                GameGlobal.unityNamespace.SendMessage(callbackObj, callbackMethod, mockData);
            } else if (typeof unityInstance !== 'undefined') {
                unityInstance.SendMessage(callbackObj, callbackMethod, mockData);
            }
        }
    },

    // ========================================
    // wx.getSystemInfoSync — 获取系统信息（同步）
    // ========================================
    WX_GetSystemInfoSync: function() {
        console.log('[WXBridge.jslib] 调用 wx.getSystemInfoSync...');
        
        var result;
        if (typeof wx !== 'undefined' && wx.getSystemInfoSync) {
            var info = wx.getSystemInfoSync();
            result = JSON.stringify(info);
        } else {
            result = JSON.stringify({
                brand: 'Browser',
                model: 'Web',
                platform: 'web'
            });
        }
        
        // 将JS字符串转为Unity可读的指针
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    }
});
