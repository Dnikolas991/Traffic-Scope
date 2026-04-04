/**
 * 本地化适配器
 * 
 * 修复：cs2/l10n 模块不直接导出 l10n 对象，而是提供 useLocalization (useCachedLocalization) Hook。
 */

import { useLocalization } from "cs2/l10n";
import { useCallback } from "react";

/**
 * 自定义 Hook，用于在组件中获取翻译函数
 */
export const useTranslate = () => {
    const { translate: gameTranslate } = useLocalization();

    /**
     * 翻译执行函数
     * @param key 翻译键值
     * @param fallback 找不到键值时的后备文本
     * @param arg 可选参数，用于替换 {0}
     */
    return useCallback((key: string | undefined, fallback: string, arg?: string): string => {
        if (!key) return fallback;

        // 调用游戏原生的 translate 方法
        const translated = gameTranslate(key, fallback);

        if (!translated || translated === key) {
            return fallback;
        }

        // 处理 {0} 占位符替换
        if (arg !== undefined) {
            return translated.replace("{0}", arg);
        }

        return translated;
    }, [gameTranslate]);
};
