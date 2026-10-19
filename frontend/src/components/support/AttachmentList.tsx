import React, { useState, useRef, useEffect, useCallback } from 'react';
import { FileText, Image as ImageIcon, ExternalLink, Download, X } from 'lucide-react';
import { AttachmentMetadata } from '../../types';
import { useI18n } from '../../i18n/I18nContext';

interface AttachmentListProps {
  attachments?: AttachmentMetadata[];
  title?: string;
  loadAttachmentBlob?: (attachment: AttachmentMetadata) => Promise<Blob>;
}

export const AttachmentList: React.FC<AttachmentListProps> = ({
  attachments,
  title,
  loadAttachmentBlob,
}) => {
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const displayTitle =
    title !== undefined
      ? title
      : (isVi ? 'Tệp đính kèm / Bằng chứng' : 'Attachments / Evidence');
  const [activePreviewImage, setActivePreviewImage] = useState<{ url: string; name: string; isBlobUrl?: boolean } | null>(null);
  const createdBlobUrlsRef = useRef<Set<string>>(new Set());

  // Clean up object URLs on component unmount
  useEffect(() => {
    const urls = createdBlobUrlsRef.current;
    return () => {
      urls.forEach((url) => {
        try {
          URL.revokeObjectURL(url);
        } catch {
          // ignore
        }
      });
      urls.clear();
    };
  }, []);

  const closePreview = useCallback(() => {
    setActivePreviewImage((prev) => {
      if (prev?.isBlobUrl) {
        try {
          URL.revokeObjectURL(prev.url);
          createdBlobUrlsRef.current.delete(prev.url);
        } catch {
          // ignore
        }
      }
      return null;
    });
  }, []);

  if (!attachments || attachments.length === 0) return null;

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const handleOpenAttachment = async (att: AttachmentMetadata) => {
    let url = att.dataUrl;
    let isCreatedBlobUrl = false;
    if (!url) {
      if (!loadAttachmentBlob) {
        return;
      }
      try {
        const blob = await loadAttachmentBlob(att);
        url = URL.createObjectURL(blob);
        isCreatedBlobUrl = true;
        createdBlobUrlsRef.current.add(url);
      } catch (e) {
        console.error('Failed to load attachment blob', e);
        return;
      }
    }
    const isPdf = att.type === 'application/pdf' || att.name.toLowerCase().endsWith('.pdf');
    if (!isPdf) {
      // If replacing an existing preview image that was a blob URL, clean up the previous one
      setActivePreviewImage((prev) => {
        if (prev?.isBlobUrl && prev.url !== url) {
          try {
            URL.revokeObjectURL(prev.url);
            createdBlobUrlsRef.current.delete(prev.url);
          } catch {
            // ignore
          }
        }
        return { url, name: att.name, isBlobUrl: isCreatedBlobUrl };
      });
    } else {
      const win = window.open(url, '_blank');
      if (!win) {
        const a = document.createElement('a');
        a.href = url;
        a.download = att.name;
        a.target = '_blank';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
      }
      if (isCreatedBlobUrl) {
        setTimeout(() => {
          try {
            URL.revokeObjectURL(url);
            createdBlobUrlsRef.current.delete(url);
          } catch {
            // ignore
          }
        }, 10000);
      }
    }
  };

  return (
    <div className="space-y-2 pt-2 border-t border-slate-100 dark:border-slate-800">
      <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider flex items-center gap-1.5">
        <span>{displayTitle} ({attachments.length})</span>
      </div>

      <div className="flex flex-wrap gap-2">
        {attachments.map((att) => {
          const isPdf = att.type === 'application/pdf' || att.name.toLowerCase().endsWith('.pdf');
          return (
            <div
              key={att.id}
              onClick={() => handleOpenAttachment(att)}
              className="group flex items-center gap-2 px-3 py-2 rounded-xl bg-slate-100/80 hover:bg-slate-200/80 dark:bg-slate-800/80 dark:hover:bg-slate-700/80 border border-slate-200/60 dark:border-slate-700/80 transition-all cursor-pointer text-xs"
              title={isVi ? 'Nhấn để xem trước hoặc tải tệp' : 'Click to preview or download'}
            >
              {isPdf ? (
                <FileText className="w-4 h-4 text-rose-500 shrink-0" />
              ) : (
                <ImageIcon className="w-4 h-4 text-emerald-500 shrink-0" />
              )}
              <span className="font-semibold text-slate-700 dark:text-slate-200 max-w-[150px] truncate">
                {att.name}
              </span>
              <span className="text-[10px] text-slate-400 font-mono">
                ({formatFileSize(att.size)})
              </span>
              <ExternalLink className="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity ml-0.5" />
            </div>
          );
        })}
      </div>

      {/* Image Preview Lightbox Modal */}
      {activePreviewImage && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-xs animate-in fade-in"
          onClick={closePreview}
        >
          <div
            className="relative max-w-3xl max-h-[90vh] bg-white dark:bg-slate-900 rounded-2xl overflow-hidden shadow-2xl p-2 flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between p-3 border-b border-slate-100 dark:border-slate-800 text-xs font-bold text-slate-800 dark:text-slate-200">
              <span className="truncate pr-4">{activePreviewImage.name}</span>
              <div className="flex items-center gap-2">
                <a
                  href={activePreviewImage.url}
                  download={activePreviewImage.name}
                  className="p-1.5 rounded-lg text-slate-500 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                  title={isVi ? 'Tải ảnh về máy' : 'Download image'}
                >
                  <Download className="w-4 h-4" />
                </a>
                <button
                  type="button"
                  onClick={closePreview}
                  className="p-1.5 rounded-lg text-slate-500 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
            </div>
            <div className="p-2 overflow-auto max-h-[calc(90vh-60px)] flex items-center justify-center bg-slate-50 dark:bg-slate-950 rounded-xl">
              <img
                src={activePreviewImage.url}
                alt={activePreviewImage.name}
                className="max-h-[75vh] w-auto object-contain rounded-lg shadow-xs"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
