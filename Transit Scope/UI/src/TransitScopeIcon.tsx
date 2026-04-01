import React from "react";

interface Props {
    active: boolean;
}

export const TransitScopeIcon = ({ active }: Props) => {
    const color = active ? "#F4FAFF" : "#D6DEE7";

    return (
        <svg
            viewBox="0 0 24 24"
            width="24"
            height="24"
            fill="none"
            stroke={color}
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <circle cx="9.5" cy="9.5" r="4.25" />
            <path d="M12.7 12.7L17.2 17.2" />
            <path d="M4 19.2H10.5" />
            <path d="M6.5 17V21.4" />
            <path d="M14.2 20.8V15.5" />
            <path d="M17.1 20.8V13.8" />
            <path d="M20 20.8V16.6" />
            <path d="M13.2 20.8H21" />
        </svg>
    );
};
