import enUS from "../../lang/en-US.json";
import zhHans from "../../lang/zh-HANS.json";
import deDE from "../../lang/de-DE.json";

type Dictionary = Record<string, string>;

const dictionaries: Record<string, Dictionary> = {
    en: enUS,
    zh: zhHans,
    de: deDE
};

/**
 * 根据浏览器/游戏当前语言挑选最接近的字典。
 * 这里优先支持英语、简体中文和德语，其它语言回退到英文。
 */
export const getCurrentDictionary = (): Dictionary => {
    const locale = (window.navigator.language || "en-US").toLowerCase();

    if (locale.startsWith("zh")) {
        return dictionaries.zh;
    }

    if (locale.startsWith("de")) {
        return dictionaries.de;
    }

    return dictionaries.en;
};

/**
 * 简单的单参数格式化，支持 `{0}` 占位符。
 */
export const translate = (key: string | undefined, fallback: string, arg?: string): string => {
    if (!key) {
        return fallback;
    }

    const dictionary = getCurrentDictionary();
    const template = dictionary[key] || fallback || key;
    return arg !== undefined ? template.replace("{0}", arg) : template;
};
