import React from "react";

interface Props {
    active: boolean;
}

export const SelectionIcon = ({ active }: Props) => {
    const color = active ? "#F3FBFF" : "#D9EEF8";

    return (
        <svg viewBox="0 0 64 64" width="34" height="34" fill="none" aria-hidden="true">
            <path
                d="M44.582,34.251V14.27c0-5.4-5.372-9.77-10.77-9.77s-10.77,4.372-10.77,9.77V38.7c0,2.687-1.2,3.885-3.885,3.885s-3.885-1.2-3.885-3.885V18.716h3.885L11.828,4.5,4.5,18.716H8.385V38.7c0,5.4,5.372,9.77,10.77,9.77s10.77-4.372,10.77-9.77V14.27c0-2.687,1.2-3.885,3.885-3.885s3.885,1.2,3.885,3.885v19.98c-3.358,1.19-4.372,4.626-3.768,8.137a7.323,7.323,0,0,0,14.539-1.249C48.467,37.964,47.415,35.277,44.582,34.251Z"
                transform="translate(58.483 5.517) rotate(90)"
                fill={color}
            />
        </svg>
    );
};
